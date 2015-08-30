using Akka.Actor;
using Akka.Event;
using Even.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Even
{
    /// <summary>
    /// Represents a stream of events for a projection.
    /// 
    /// There is a single ProjectionStream for each projection query in the system. This actor
    /// forwards events in sequence for the stream and also takes care of replaying messages
    /// to subscribers in the correct order.
    /// </summary>
    public class ProjectionStream : ReceiveActor, IWithUnboundedStash
    {
        string _projectionId;
        IStreamPredicate[] _predicates;
        IActorRef _eventReader;
        IActorRef _indexWriter;
        LinkedList<IActorRef> _subscribers = new LinkedList<IActorRef>();

        Guid _replayId;

        int _sequence;
        long _checkpoint;

        // TODO: read this from settings
        TimeSpan _replayTimeout = TimeSpan.FromSeconds(15);

        public ILoggingAdapter Log = Context.GetLogger();
        public IStash Stash { get; set; }

        public ProjectionStream()
        {
            Become(Uninitialized);
        }

        #region States

        private void Uninitialized()
        {
            Receive<InitializeProjectionStream>(ini =>
            {
                // store work variables
                _projectionId = ini.Query.StreamID;
                _predicates = ini.Query.Predicates.ToArray();
                _eventReader = ini.EventReader;
                _indexWriter = ini.IndexWriter;

                // subscribe to events in the stream
                Context.System.EventStream.Subscribe(Self, typeof(IEvent));

                // request the replay
                _replayId = Guid.NewGuid();

                _eventReader.Tell(new ProjectionStreamReplayRequest
                {
                    ReplayID = _replayId,
                    ProjectionID = _projectionId,
                    MaxCheckpoint = Int64.MaxValue,
                    SendIndexedEvents = false
                });

                Become(ReplayFromIndex);
            });
        }

        private void ReplayFromIndex()
        {
            SetReceiveTimeout(_replayTimeout);

            // the stream only receives the last known index state from the reader
            Receive<ProjectionStreamIndexReplayCompleted>(msg =>
            {
                _sequence = msg.LastSeenSequence;
                _checkpoint = msg.LastSeenCheckpoint;

                Become(ReplayFromEvents);

            }, msg => msg.ReplayID == _replayId);

            Receive(new Action<ReceiveTimeout>(_ =>
            {
                throw new Exception("Replay Timeout");
            }));

            ReceiveAny(o => Stash.Stash());
        }

        private void ReplayFromEvents()
        {
            Stash.UnstashAll();
            SetReceiveTimeout(_replayTimeout);

            Receive<ReplayEvent>(e =>
            {
                ReceiveEvent(e.Event);

            }, e => e.ReplayID == _replayId);

            Receive<ReplayCompleted>(_ =>
            {
                // clear the replay id
                _replayId = Guid.Empty;

                // remove the timeout handler
                SetReceiveTimeout(null);

                // switch to ready state
                Become(Ready);

            }, e => e.ReplayID == _replayId);

            // on errors, let the stream restart
            Receive<ReplayCancelled>(msg =>
            {
                throw new Exception("Replay was cancelled.");

            }, e => e.ReplayID == _replayId);

            Receive<ReplayAborted>(msg =>
            {
                throw new Exception("Replay was aborted.", msg.Exception);

            }, e => e.ReplayID == _replayId);

            Receive(new Action<ReceiveTimeout>(_ =>
            {
                throw new Exception("Replay Timeout");
            }));
        }

        private void Ready()
        {
            Stash.UnstashAll();

            Receive(new Action<IEvent>(ReceiveEvent));

            // receive subscription requests
            Receive<ProjectionSubscriptionRequest>(ps =>
            {
                _subscribers.AddLast(Sender);
                Context.Watch(Sender);

                // if the subscriber is out of date, create a worker to replay
                // all events until the moment of this subscription as the next
                // events will be forwarded automatically
                if (ps.LastKnownSequence < _sequence)
                {
                    var actor = Context.ActorOf<ProjectionReplayProxy>();

                    actor.Tell(new ProjectionReplayProxy.Initializer
                    {
                        EventReader = _eventReader,
                        ProjectionID = _projectionId,
                        InitialSequence = ps.LastKnownSequence + 1,
                        Subscriber = Sender,
                        Checkpoint = _checkpoint,
                        Predicates = _predicates.ToArray()
                    });
                }

            }, ps => ps.Query.StreamID == _projectionId);

            // unsubscribe terminated projections
            Receive<Terminated>(t =>
            {
                var node = _subscribers.First;

                while (node != null)
                {
                    if (node.Value.Equals(t.ActorRef))
                    {
                        _subscribers.Remove(node);
                        break;
                    }

                    node = node.Next;
                }
            });
        }

        #endregion

        private void ReceiveEvent(IEvent e)
        {
            var expected = _checkpoint + 1;
            var received = e.Checkpoint;

            // if the checkpoint order matches
            if (received == expected)
            {
                // and the event matches que query, emit it
                if (EventMatches(e))
                    Emit(e);

                // if we received events out of order before, unstash
                Stash.UnstashAll();

                return;
            }

            // if it's a future checkpoint, stash until we get the right one
            if (received > expected)
                Stash.Stash();
        }

        private bool EventMatches(IEvent streamEvent)
        {
            return true;
        }

        protected virtual void Emit(IEvent @event)
        {
            _sequence++;

            // tell the subscribers
            var projectionEvent = new ProjectionEvent(_projectionId, _sequence, @event);

            foreach (var s in _subscribers)
                s.Tell(projectionEvent);

            // index the event
            _indexWriter.Tell(new PersistProjectionIndexRequest
            {
                ProjectionID = _projectionId,
                ProjectionSequence = _sequence,
                Checkpoint = _checkpoint
            });
        }

        /// <summary>
        /// This worker is a proxy to replay events to the projection stream subscriber.
        /// 
        /// When subscribers first subscribe and their state is older than the streams's state,
        /// we ask the reader to get the events from the stored index. Since the index may
        /// be out of date due to delayed writes, we rebuild the state from the global event
        /// stream if needed.
        /// </summary>
        class ProjectionReplayProxy : ReceiveActor, IWithUnboundedStash
        {
            public IStash Stash { get; set; }

            string _projectionId;
            IActorRef _subscriber;
            int _sequence;
            long _checkpoint;
            TimeSpan _replayTimeout = TimeSpan.FromSeconds(15);

            Guid _replayId;
            Guid _subscriberReplayId;
            IStreamPredicate[] _predicates;

            public ProjectionReplayProxy()
            {
                Receive<Initializer>(ini =>
                {
                    // store some work variables
                    _subscriber = ini.Subscriber;
                    _projectionId = ini.ProjectionID;
                    _sequence = ini.InitialSequence;
                    _predicates = ini.Predicates;
                    _checkpoint = 0;

                    // set the new replay id and request data to the reader
                    _replayId = Guid.NewGuid();

                    ini.EventReader.Tell(new ProjectionStreamReplayRequest
                    {
                        ReplayID = _replayId,
                        ProjectionID = ini.ProjectionID,
                        InitialSequence = ini.InitialSequence,
                        MaxCheckpoint = ini.Checkpoint,
                        SendIndexedEvents = true
                    });

                    // switch to another state waiting for indexed events
                    Become(ReplayFromIndex);
                });
            }

            /// <summary>
            /// Receives events from the index until all the index is read.
            /// </summary>
            void ReplayFromIndex()
            {
                SetReceiveTimeout(_replayTimeout);

                // matches events read from stream
                Receive<ProjectionReplayEvent>(e =>
                {
                    // ensure we're emitting events in the right order
                    var received = e.Event.ProjectionSequence;
                    var expected = _sequence + 1;

                    // if the order matches, forward the event to the subscriber
                    if (received == expected)
                    {
                        // update the virtual stream state
                        _sequence = expected;
                        _checkpoint = e.Event.Checkpoint;

                        _subscriber.Tell(new ProjectionReplayEvent
                        {
                            ReplayID = _subscriberReplayId,
                            Event = e.Event
                        });

                        Stash.UnstashAll();

                        return;
                    }

                    // if it's newer, stash until the correct one is received
                    if (received > expected)
                        Stash.Stash();

                }, e => e.ReplayID == _replayId);

                // when the replay is completed
                Receive<ProjectionStreamIndexReplayCompleted>(e =>
                {
                    // ensure we're switching to rebuild in the right order
                    var received = e.LastSeenSequence;
                    var expected = _sequence;

                    if (received == expected)
                    {
                        // update the last known checkpoint
                        _checkpoint = e.LastSeenCheckpoint;
                        Become(ReplayFromEvents);
                        return;
                    }

                    if (received > expected)
                        Stash.Stash();

                }, e => e.ReplayID == _replayId);

                Receive<ReceiveTimeout>(_ => HandleReplayTimeout());
            }

            /// <summary>
            /// Recreates events from the global stream until the replay is completed.
            /// </summary>
            void ReplayFromEvents()
            {
                SetReceiveTimeout(_replayTimeout);
                Stash.UnstashAll();

                Receive<ReplayEvent>(e =>
                {
                    // ensure the events are checked in the right order
                    var received = e.Event.Checkpoint;
                    var expected = _checkpoint + 1;

                    if (received == expected)
                    {
                        // forward the event only if it matches the stream query
                        if (EventMatches(e.Event))
                        {
                            _sequence++;

                            var replayEvent = new ProjectionReplayEvent
                            {
                                ReplayID = _subscriberReplayId,
                                Event = new ProjectionEvent(_projectionId, _sequence, e.Event)
                            };
                            
                            _subscriber.Tell(replayEvent);
                        }

                        Stash.UnstashAll();

                        return;
                    }

                    if (received > expected)
                        Stash.Stash();

                }, e => e.ReplayID == _replayId);

                Receive<ReplayCompleted>(e =>
                {
                    // ensure the replay finished in the right order
                    if (e.LastCheckpoint == _checkpoint)
                    {
                        // notify the subscriber and stop the worker
                        _subscriber.Tell(new ProjectionReplayCompleted {
                            ReplayID = _subscriberReplayId,
                            LastCheckpoint = _checkpoint,
                            LastSequence = _sequence
                        });

                        Context.Stop(Self);
                        return;
                    }

                    if (e.LastCheckpoint > _checkpoint)
                        Stash.Stash();

                }, e => e.ReplayID == _replayId);

                Receive<ReceiveTimeout>(_ => HandleReplayTimeout());
            }

            private bool EventMatches(IEvent e)
            {
                foreach (var p in _predicates)
                    if (p.EventMatches(e))
                        return true;

                return false;
            }

            private void HandleReplayTimeout()
            {
                _subscriber.Tell(new ReplayAborted { ReplayID = _subscriberReplayId, Message = "Timeout" });
                Context.Stop(Self);
            }

            public class Initializer
            {
                public string ProjectionID { get; set; }
                public int InitialSequence { get; set; }
                public IActorRef EventReader { get; set; }
                public IActorRef Subscriber { get; set; }
                public long Checkpoint { get; set; }
                public Func<IEvent, bool> EventMatches { get; set; }
                public IStreamPredicate[] Predicates { get; set; }
            }
        }
    }
}
