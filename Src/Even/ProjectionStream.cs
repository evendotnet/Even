using Akka.Actor;
using Akka.Event;
using Even.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
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
        string _projectionStreamId;
        IProjectionStreamPredicate[] _predicates;
        IActorRef _eventReader;
        IActorRef _writer;
        LinkedList<IActorRef> _subscribers = new LinkedList<IActorRef>();

        Guid _replayId;

        int _projectionStreamSequence;
        long _globalSequence;
        bool _isReplaying = true;
        int _lastIndexedSequence;

        // TODO: read this from settings
        TimeSpan _replayTimeout = TimeSpan.FromHours(15);

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
                _projectionStreamId = ini.Query.ProjectionStreamID;
                _predicates = ini.Query.Predicates.ToArray();
                _eventReader = ini.Reader;
                _writer = ini.Writer;

                // subscribe to events in the stream
                Context.System.EventStream.Subscribe(Self, typeof(IPersistedEvent));

                // request the replay
                _replayId = Guid.NewGuid();

                _eventReader.Tell(new ProjectionStreamReplayRequest
                {
                    ReplayID = _replayId,
                    ProjectionID = _projectionStreamId,
                    MaxCheckpoint = Int64.MaxValue,
                    SendIndexedEvents = false
                });

                Become(ReplayFromIndex);
            });
        }

        private void ReplayFromIndex()
        {
            SetReceiveTimeout(_replayTimeout);
            Log.Debug("{0}: Projection Stream starting replay", _projectionStreamId);

            // the stream only receives the last known index state from the reader
            Receive<ProjectionStreamIndexReplayCompleted>(msg =>
            {
                Log.Debug("{0}: Projection Index Replay Complete", _projectionStreamId);

                _projectionStreamSequence = _lastIndexedSequence = msg.LastSeenProjectionStreamSequence;
                _globalSequence = msg.LastSeenGlobalSequence;

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
                ReceiveEventInternal(e.Event);

            }, e => e.ReplayID == _replayId);

            Receive<ReplayCompleted>(_ =>
            {
                Log.Debug("{0}: Projection Event Replay Complete", _projectionStreamId);

                _isReplaying = false;

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

            ReceiveAny(o => Stash.Stash());
        }

        private void Ready()
        {
            Log.Debug("{0}: Projection Stream Ready", _projectionStreamId);

            Stash.UnstashAll();

            Receive(new Action<IPersistedEvent>(ReceiveEventInternal));

            // receive subscription requests
            Receive<ProjectionSubscriptionRequest>(ps =>
            {
                _subscribers.AddLast(Sender);
                Context.Watch(Sender);

                // if the subscriber is out of date, create a worker to replay
                // all events until the moment of this subscription as the next
                // events will be forwarded automatically
                if (ps.LastKnownSequence < _projectionStreamSequence)
                {
                    var actor = Context.ActorOf<ProjectionReplayProxy>();

                    actor.Tell(new ProjectionReplayProxy.Initializer
                    {
                        EventReader = _eventReader,
                        ProjectionID = _projectionStreamId,
                        InitialSequence = ps.LastKnownSequence + 1,
                        Subscriber = Sender,
                        SubscriberReplayID = ps.ReplayID,
                        Checkpoint = _globalSequence,
                        Predicates = _predicates.ToArray()
                    });
                }
                // otherwise just send a completed message to the projection
                // with the current state
                else
                {
                    Sender.Tell(new ProjectionReplayCompleted
                    {
                        ReplayID = ps.ReplayID,
                        LastSeenGlobalSequence = _globalSequence,
                        LastSeenProjectionStreamSequence = _projectionStreamSequence
                    });
                }

            }, ps => ps.Query.ProjectionStreamID == _projectionStreamId);

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

        private void ReceiveEventInternal(IPersistedEvent e)
        {
            Contract.Assert(e.GlobalSequence == _globalSequence + 1);

            _globalSequence++;

            // and the event matches que query, emit it
            if (EventMatches(e))
            {
                // increment the sequence
                _projectionStreamSequence++;
                Emit(e);
            }
        }

        private bool EventMatches(IPersistedEvent streamEvent)
        {
            foreach (var p in _predicates)
                if (p.EventMatches(streamEvent))
                    return true;

            return false;
        }

        protected virtual void Emit(IPersistedEvent @event)
        {
            if (!_isReplaying)
            {
                // tell the subscribers
                var projectionEvent = ProjectionEventFactory.Create(_projectionStreamId, _projectionStreamSequence, @event);

                foreach (var s in _subscribers)
                    s.Tell(projectionEvent);
            }

            if (_projectionStreamSequence > _lastIndexedSequence)
            {
                // index the event
                _writer.Tell(new ProjectionIndexPersistenceRequest
                {
                    ProjectionStreamID = _projectionStreamId,
                    Entry = new IndexSequenceEntry
                    {
                        GlobalSequence = _globalSequence,
                        ProjectionStreamSequence = _projectionStreamSequence
                    }
                });
            }
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

            string _projectionStreamId;
            IActorRef _subscriber;
            int _projectionStreamSequence;
            long _globalSequence;
            TimeSpan _replayTimeout = TimeSpan.FromHours(15);

            Guid _replayId;
            Guid _subscriberReplayId;
            IProjectionStreamPredicate[] _predicates;
            ILoggingAdapter Log = Context.GetLogger();

            public ProjectionReplayProxy()
            {
                Receive<Initializer>(ini =>
                {
                    // store some work variables
                    _subscriber = ini.Subscriber;
                    _projectionStreamId = ini.ProjectionID;
                    _projectionStreamSequence = 0;
                    _predicates = ini.Predicates;
                    _globalSequence = 0;
                    _subscriberReplayId = ini.SubscriberReplayID;

                    // set the new replay id and request data to the reader
                    _replayId = Guid.NewGuid();

                    Log.Debug("Starting projection replay for " + _subscriber);

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
                Log.Debug("Proxy Replay from Index");

                SetReceiveTimeout(_replayTimeout);

                // matches events read from stream
                Receive<ProjectionReplayEvent>(e =>
                {
                    Contract.Assert(e.Event.ProjectionStreamSequence == _projectionStreamSequence + 1);

                    _projectionStreamSequence++;
                    _globalSequence = e.Event.GlobalSequence;

                    _subscriber.Tell(new ProjectionReplayEvent
                    {
                        ReplayID = _subscriberReplayId,
                        Event = e.Event
                    });

                }, e => e.ReplayID == _replayId);

                // when the replay is completed
                Receive<ProjectionStreamIndexReplayCompleted>(e =>
                {
                    Contract.Assert(e.LastSeenProjectionStreamSequence == _projectionStreamSequence);

                    _globalSequence = e.LastSeenGlobalSequence;
                    Become(ReplayFromEvents);

                }, e => e.ReplayID == _replayId);

                Receive<ReceiveTimeout>(_ => HandleReplayTimeout());
            }

            /// <summary>
            /// Recreates events from the global stream until the replay is completed.
            /// </summary>
            void ReplayFromEvents()
            {
                Log.Debug("Proxy Replay from Events");

                SetReceiveTimeout(_replayTimeout);
                Stash.UnstashAll();

                Receive<ReplayEvent>(e =>
                {
                    Contract.Assert(e.Event.GlobalSequence == _globalSequence + 1);

                    _globalSequence++;

                    // forward the event only if it matches the stream query
                    if (EventMatches(e.Event))
                    {
                        _projectionStreamSequence++;

                        var replayEvent = new ProjectionReplayEvent
                        {
                            ReplayID = _subscriberReplayId,
                            Event = ProjectionEventFactory.Create(_projectionStreamId, _projectionStreamSequence, e.Event)
                        };
                            
                        _subscriber.Tell(replayEvent);
                    }

                }, e => e.ReplayID == _replayId);

                Receive<ReplayCompleted>(e =>
                {
                    Contract.Assert(e.LastSeenGlobalSequence == _globalSequence);

                    // notify the subscriber and stop the worker
                    _subscriber.Tell(new ProjectionReplayCompleted {
                        ReplayID = _subscriberReplayId,
                        LastSeenGlobalSequence = _globalSequence,
                        LastSeenProjectionStreamSequence = _projectionStreamSequence
                    });

                    Context.Stop(Self);
                    return;
                    
                }, e => e.ReplayID == _replayId);

                Receive<ReceiveTimeout>(_ => HandleReplayTimeout());
            }

            private bool EventMatches(IPersistedEvent e)
            {
                foreach (var p in _predicates)
                    if (p.EventMatches(e))
                        return true;

                return false;
            }

            private void HandleReplayTimeout()
            {
                _subscriber.Tell(new ReplayAborted { ReplayID = _subscriberReplayId });
                Context.Stop(Self);
            }

            public class Initializer
            {
                public string ProjectionID { get; set; }
                public Guid SubscriberReplayID { get; set; }
                public int InitialSequence { get; set; }
                public IActorRef EventReader { get; set; }
                public IActorRef Subscriber { get; set; }
                public long Checkpoint { get; set; }
                public IProjectionStreamPredicate[] Predicates { get; set; }
            }
        }
    }
}
