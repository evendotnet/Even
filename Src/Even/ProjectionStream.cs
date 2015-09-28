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
        ProjectionStreamQuery _query;
        IActorRef _reader;
        IActorRef _writer;
        LinkedList<IActorRef> _subscribers = new LinkedList<IActorRef>();
        GlobalOptions _options;

        // used during replay
        Guid _lastRequestId;
        int _requestedEvents;
        int _readEvents;

        long _globalSequence;
        int _currentSequence;

        public IStash Stash { get; set; }

        public ProjectionStream(ProjectionStreamQuery query, IActorRef reader, IActorRef writer, GlobalOptions options)
        {
            Argument.RequiresNotNull(query, nameof(query));
            Argument.RequiresNotNull(reader, nameof(reader));
            Argument.RequiresNotNull(writer, nameof(writer));
            Argument.RequiresNotNull(options, nameof(options));

            // subscribe to events in the stream
            Context.System.EventStream.Subscribe(Self, typeof(IPersistedEvent));

            // request checkpoint
            var request = new ReadProjectionIndexCheckpointRequest(_query.ProjectionStreamID);
            _lastRequestId = request.RequestID;
            _reader.Tell(request);

            Become(AwaitingCheckpoint);
        }

        #region States

        private void AwaitingCheckpoint()
        {
            SetReceiveTimeout(_options.ReadRequestTimeout);

            Receive<ReadProjectionIndexCheckpointResponse>(m =>
            {
                _globalSequence = m.LastSeenGlobalSequence;
                StartReadRequest();
                Become(ReplayingEvents);

            }, m => m.RequestID == _lastRequestId);

            Receive(new Action<Aborted>(m =>
            {
                throw new Exception("The replay was aborted.", m.Exception);
            }));

            Receive(new Action<ReceiveTimeout>(_ =>
            {
                throw new TimeoutException("Timeout while awaiting for projection checkpoint.");
            }));

            ReceiveAny(_ => Stash.Stash());
        }

        private void ReplayingEvents()
        {
            SetReceiveTimeout(_options.ReadRequestTimeout);

            Receive<ReadResponse>(m =>
            {
                if (m.RequestID != _lastRequestId)
                    return;
                
                _readEvents++;
                ReceiveEventInternal(m.Event, false);
            });

            Receive<ReadFinished>(m =>
            {
                if (m.RequestID != _lastRequestId)
                    return;
                
                // if the read events are at least the amount requested
                // there might be more events to be read
                if (_requestedEvents != EventCount.Unlimited && _readEvents >= _requestedEvents)
                {
                    StartReadRequest();
                }
                // otherwise we read all events, just becode ready
                else
                {
                    //cleanup temp vars
                    _readEvents = _requestedEvents = 0;
                    Stash.UnstashAll();
                    Become(Ready);
                }
            });

            Receive(new Action<Aborted>(m =>
            {
                throw new Exception("The replay was aborted.", m.Exception);
            }));

            Receive(new Action<ReceiveTimeout>(_ =>
            {
                throw new TimeoutException("Timeout while replaying events.");
            }));

            ReceiveAny(_ => Stash.Stash());
        }

        private void Ready()
        {
            Receive<IPersistedEvent>(e => ReceiveEventInternal(e, true));

            // receive subscription requests
            Receive<ProjectionSubscriptionRequest>(ps =>
            {
                _subscribers.AddLast(Sender);
                Context.Watch(Sender);

                // if the subscriber is out of date, create a worker to replay
                // all events until the moment of this subscription as the next
                // events will be forwarded automatically
                if (ps.LastKnownSequence < _currentSequence)
                {
                    StartProjectionReplay(ps);
                }
                // otherwise just send a completed message to the projection
                // with the current state
                else
                {
                    Sender.Tell(new ProjectionReplayFinished(ps.RequestID));
                }

            }, ps => _query.Equals(ps.Query));

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

        void StartReadRequest()
        {
            var request = new ReadRequest(_globalSequence + 1, _options.EventsPerReadRequest);
            _reader.Tell(request);

            _requestedEvents = request.Count;
            _lastRequestId = request.RequestID;
            _readEvents = 0;
        }

        private void ReceiveEventInternal(IPersistedEvent e, bool tellSubscribers)
        {
            // and the event matches the query, emit it
            if (EventMatches(e))
            {
                // increment the sequence
                _currentSequence++;

                // index the event
                _writer.Tell(new ProjectionIndexPersistenceRequest(_query.ProjectionStreamID, _currentSequence, e.GlobalSequence));

                // tell the subscribers
                if (tellSubscribers)
                {
                    var projectionEvent = ProjectionEventFactory.Create(_query.ProjectionStreamID, _currentSequence, e);

                    foreach (var s in _subscribers)
                        s.Tell(projectionEvent);
                }
            }
        }

        private bool EventMatches(IPersistedEvent @event)
        {
            foreach (var p in _query.Predicates)
                if (p.EventMatches(@event))
                    return true;

            return false;
        }

        private void StartProjectionReplay(ProjectionSubscriptionRequest ps)
        {
            var props = Props
                .Create<ProjectionReplayWorker>(_reader, Sender, ps, _currentSequence - 1, _options)
                .WithSupervisorStrategy(new OneForOneStrategy(ex => Directive.Stop));

            Context.ActorOf(props);
        }
    }

    /// <summary>
    /// This worker is a proxy to replay events to the projection stream subscriber.
    /// 
    /// When subscribers first subscribe and their state is older than the streams's state,
    /// we ask the reader to get the events from the stored index. If not all events are
    /// in the index, retry until the index has the required events.
    /// </summary>
    public class ProjectionReplayWorker : ReceiveActor
    {
        IActorRef _reader;
        IActorRef _subscriber;
        Guid _subscriberRequestId;
        string _projectionStreamId;
        int _lastSequenceToRead;
        GlobalOptions _options;

        int _currentSequence;

        Guid _lastRequestId;
        int _readEvents;

        public ProjectionReplayWorker(IActorRef reader, IActorRef subscriber, ProjectionSubscriptionRequest subscriptionRequest, int lastSequenceToRead, GlobalOptions options)
        {
            _reader = reader;
            _subscriber = subscriber;
            _lastSequenceToRead = lastSequenceToRead;
            _options = options;

            _subscriberRequestId = subscriptionRequest.RequestID;
            _projectionStreamId = subscriptionRequest.Query.ProjectionStreamID;
            _currentSequence = subscriptionRequest.LastKnownSequence;

            StartReadRequest();
            Become(Replaying);
        }

        void StartReadRequest()
        {
            var maxEvents = _options.EventsPerReadRequest >= 0 ? _options.EventsPerReadRequest : 0;
            var count = Math.Max(_lastSequenceToRead - _currentSequence, maxEvents);

            var readRequest = new ReadIndexedProjectionStreamRequest(_projectionStreamId, _currentSequence + 1, count);
            _reader.Tell(readRequest);

            _lastRequestId = readRequest.RequestID;
            _readEvents = 0;
        }

        private void Replaying()
        {
            SetReceiveTimeout(_options.ReadRequestTimeout);

            Receive<ReadIndexedProjectionStreamResponse>(m =>
            {
                if (m.Event.StreamSequence <= _lastSequenceToRead)
                {
                    _currentSequence = m.Event.StreamSequence;
                    var replayEvent = new ProjectionReplayEvent(_subscriberRequestId, m.Event);
                    _subscriber.Tell(replayEvent);
                }

            }, m => m.RequestID == _lastRequestId);

            Receive<ReadIndexedProjectionStreamFinished>(m =>
            {
                if (_currentSequence < _lastSequenceToRead)
                {
                    Context.System.Scheduler.ScheduleTellOnce(_options.ProjectionReplayRetryInterval, Self, new Retry(), Self);
                }
                else
                {
                    _subscriber.Tell(new ProjectionReplayFinished(_subscriberRequestId));
                    Context.Stop(Self);
                }

            }, m => m.RequestID == _lastRequestId);

            Receive<Retry>(_ =>
            {
                StartReadRequest();
            });

            Receive<CancelRequest>(m =>
            {
                Sender.Tell(new Cancelled(m.RequestID));
                Context.Stop(Self);

            }, m => m.RequestID == _subscriberRequestId);

            Receive(new Action<ReceiveTimeout>(_ =>
            {
                var ex = new TimeoutException("Timeout during projection replay.");
                _subscriber.Tell(new Aborted(_subscriberRequestId, ex));
                throw ex;
            }));
        }

        class Retry { }
    }
}
