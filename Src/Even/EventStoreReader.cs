using Akka.Actor;
using Akka.Event;
using Even.Messages;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace Even
{
    public class EventStoreReader : ReceiveActor
    {
        IEventStoreReader _storeReader;
        ISerializer _serializer;
        ILoggingAdapter _log = Context.GetLogger();
        EventRegistry _eventRegistry;

        public EventStoreReader()
        {
            Receive<InitializeEventStoreReader>(ini =>
            {
                _storeReader = ini.StoreReader;
                _serializer = ini.Serializer;
                _eventRegistry = ini.EventRegistry;

                Become(Ready);
            });
        }

        void Ready()
        {
            Receive<ReplayAggregateRequest>(request =>
            {
                var props = PropsFactory
                    .Create<AggregateReplayWorker>(_storeReader, (Deserializer)DeserializeEvent)
                    .WithSupervisorStrategy(new OneForOneStrategy(e => Directive.Stop));

                var actor = Context.ActorOf(props);
                actor.Forward(request);
            });

            Receive<ProjectionStreamReplayRequest>(request =>
            {
                var props = PropsFactory
                    .Create<ProjectionStreamReplayWorker>(_storeReader, (Deserializer)DeserializeEvent)
                    .WithSupervisorStrategy(new OneForOneStrategy(e => Directive.Stop));

                var actor = Context.ActorOf(props);
                actor.Forward(request);
            });
        }

        delegate IPersistedEvent Deserializer(string streamId, int streamSequence, IPersistedRawEvent rawEvent);

        IPersistedEvent DeserializeEvent(string streamId, int sequence, IPersistedRawEvent rawEvent)
        {
            var metadata = _serializer.DeserializeMetadata(rawEvent.Metadata);

            // try loading the type from the registry
            var clrType = _eventRegistry.GetClrType(rawEvent.EventType);

            // if it fails, try loading the metadata
            if (clrType == null && metadata != null)
            {
                object clrTypeName;

                if (metadata.TryGetValue(Constants.ClrTypeMetadataKey, out clrTypeName))
                {
                    var s = clrTypeName as string;

                    if (s != null)
                        clrType = Type.GetType(s, false, true);
                }
            }

            // if no type was found, the serializer should use its default type
            var domainEvent = _serializer.DeserializeEvent(rawEvent.Payload, rawEvent.PayloadFormat, clrType);

            return PersistedEventFactory.Create(
                rawEvent.GlobalSequence,
                rawEvent.EventID,
                streamId,
                sequence,
                rawEvent.EventType,
                rawEvent.UtcTimestamp,
                metadata,
                domainEvent
            );
        }

        class WorkerFailure
        {
            public Exception Exception { get; set; }
        }

        class WorkerReplayTaskComplete
        {
            public long LastSeenGlobalSequence { get; set; }
        }

        // base class for query and aggregate readers
        class WorkerBase : ReceiveActor
        {
            protected IActorRef OriginalSender { get; private set; }
            protected Guid ReplayID { get; private set; }
            protected ILoggingAdapter Log { get; } = Context.GetLogger();

            protected CancellationToken ReplayCancelToken => _cts.Token;
            protected bool IsReplayCancelled => _cts.IsCancellationRequested;

            // cancellation token source used to signal cancellation requests
            private CancellationTokenSource _cts = new CancellationTokenSource();

            protected void ReceiveReplayRequest<T>(Action<T> handler)
                where T : ReplayRequest
            {
                Receive<T>(request =>
                {
                    ReplayID = request.ReplayID;
                    OriginalSender = Sender;

                    handler(request);

                    Become(ReplayStarted);
                });
            }

            protected void ReplayStarted()
            {
                Receive<WorkerReplayTaskComplete>(c =>
                {
                    OriginalSender.Tell(new ReplayCompleted { ReplayID = ReplayID, LastSeenGlobalSequence = c.LastSeenGlobalSequence });
                    Context.Stop(Self);
                });

                Receive<CancelReplayRequest>(request =>
                {
                    _cts.Cancel();
                    OriginalSender.Tell(new ReplayCancelled { ReplayID = ReplayID });
                    Context.Stop(Self);

                }, request => request.ReplayID == ReplayID);

                Receive<WorkerFailure>(wf =>
                {
                    Context.GetLogger().Error(wf.Exception, "Unexpected Exception on " + this.GetType().Name);
                    _cts.Cancel();
                    OriginalSender.Tell(new ReplayAborted { ReplayID = ReplayID, Exception = wf.Exception });
                    Context.Stop(Self);

                }, _ => Self.Equals(Sender));
            }
        }

        /// <summary>
        /// Handles replay requests from aggregates.
        /// </summary>
        class AggregateReplayWorker : WorkerBase
        {
            public AggregateReplayWorker(IEventStoreReader reader, Deserializer deserialize)
            {
                ReceiveReplayRequest<ReplayAggregateRequest>(request =>
                {
                    var sender = Sender;
                    var self = Self;

                    Task.Run(async () =>
                    {
                        if (IsReplayCancelled)
                            return;
                        
                        var globalSequence = 0L;
                        var sequence = 0;

                        // normalize the query values
                        var initialSequence = request.InitialSequence > 0 ? request.InitialSequence : 1;
                        var maxEvents = -1;

                        await reader.ReadStreamAsync(request.StreamID, initialSequence, maxEvents, e =>
                        {
                            if (IsReplayCancelled)
                                return;

                            sequence++;
                            globalSequence = e.GlobalSequence;

                            var @event = deserialize(request.StreamID, sequence, e);

                            sender.Tell(new ReplayEvent
                            {
                                ReplayID = request.ReplayID,
                                Event = @event
                            }, self);

                        }, ReplayCancelToken);

                        self.Tell(new WorkerReplayTaskComplete { LastSeenGlobalSequence = globalSequence }, self);
                    })
                    .ContinueWith(t =>
                    {
                        self.Tell(new WorkerFailure { Exception = t.Exception }, self);

                    }, TaskContinuationOptions.NotOnRanToCompletion);
                });
            }
        }

        class ProjectionStreamReplayWorker : WorkerBase
        {
            public ProjectionStreamReplayWorker(IEventStoreReader reader, Deserializer deserializer)
            {
                ReceiveReplayRequest<ProjectionStreamReplayRequest>(request =>
                {
                    var sender = Sender;
                    var self = Self;

                    Task.Run(async () =>
                    {
                        if (IsReplayCancelled)
                            return;

                        // normalize request values
                        var initialSequence = request.InitialSequence > 0 ? request.InitialSequence : 1;
                        var maxEvents = request.MaxEvents > 0 && request.MaxEvents < Int32.MaxValue ? request.MaxEvents : -1;

                        // work values
                        var projectionStreamCheckpoint = 0L;
                        var indexedGlobalSequence = 0L;
                        var sequence = initialSequence - 1;

                        // check if the store supports projection indexes
                        var projectionStore = reader as IProjectionStoreReader;

                        if (projectionStore != null)
                        {
                            // if we need to send indexed events, read the events
                            if (request.SendIndexedEvents)
                            {
                                await projectionStore.ReadIndexedProjectionStreamAsync(request.StreamID, initialSequence, maxEvents, e =>
                                {
                                    if (IsReplayCancelled)
                                        return;

                                    sequence++;
                                    indexedGlobalSequence = e.GlobalSequence;

                                    var persistedEvent = deserializer(request.StreamID, sequence, e);

                                    sender.Tell(new ReplayEvent
                                    {
                                        ReplayID = request.ReplayID,
                                        Event = persistedEvent
                                    }, self);

                                }, ReplayCancelToken);
                            }
                            // otherwise we need to find out at least the highest sequences
                            else
                            {
                                sequence = await projectionStore.ReadHighestIndexedProjectionStreamSequenceAsync(request.StreamID);
                                indexedGlobalSequence = await projectionStore.ReadHighestIndexedProjectionGlobalSequenceAsync(request.StreamID);
                            }

                            // grab whatever is the checkpoint for the projection
                            projectionStreamCheckpoint = await projectionStore.ReadProjectionCheckpointAsync(request.StreamID);
                        }

                        if (IsReplayCancelled)
                            return;

                        // ensure we're seeing the last global sequence the projection stream saw
                        var globalSequence = Math.Max(projectionStreamCheckpoint, indexedGlobalSequence);

                        // sinals the end of the index and the start of the non-indexed event stream
                        sender.Tell(new ProjectionStreamIndexReplayCompleted
                        {
                            ReplayID = request.ReplayID,
                            LastSeenGlobalSequence = globalSequence,
                            LastSeenProjectionStreamSequence = sequence
                        }, self);

                        // try reading additional events from the global event stream that weren't emitted yet 
                        maxEvents = maxEvents == -1 ? -1 : request.MaxEvents - sequence;

                        if (maxEvents == -1 || maxEvents > 0)
                        {
                            await reader.ReadAsync(globalSequence + 1, maxEvents, e =>
                            {
                                if (IsReplayCancelled)
                                    return;

                                var @event = deserializer(request.StreamID, sequence, e);

                                sender.Tell(new ReplayEvent
                                {
                                    ReplayID = request.ReplayID,
                                    Event = @event

                                }, self);

                                sequence++;
                                globalSequence = e.GlobalSequence;

                            }, ReplayCancelToken);
                        }

                        self.Tell(new WorkerReplayTaskComplete { LastSeenGlobalSequence = globalSequence }, self);
                    })
                    .PipeTo(Self);
                    //.ContinueWith(t =>
                    //{
                    //    self.Tell(new WorkerFailure { Exception = t.Exception }, self);

                    //}, TaskContinuationOptions.NotOnRanToCompletion);
                });
            }
        }
    }
}
