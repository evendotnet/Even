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
        IStreamStoreReader _storeReader;
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
                    .Create<AggregateReplayWorker>(_storeReader, (Func<IPersistedRawEvent, IPersistedEvent>)DeserializeEvent)
                    .WithSupervisorStrategy(new OneForOneStrategy(e => Directive.Stop));

                var actor = Context.ActorOf(props);
                actor.Forward(request);
            });

            Receive<ProjectionStreamReplayRequest>(request =>
            {
                var props = PropsFactory
                    .Create<ProjectionStreamReplayWorker>(_storeReader, (Func<IPersistedRawEvent, IPersistedEvent>)DeserializeEvent)
                    .WithSupervisorStrategy(new OneForOneStrategy(e => Directive.Stop));

                var actor = Context.ActorOf(props);
                actor.Forward(request);
            });
        }

        IPersistedEvent DeserializeEvent(IPersistedRawEvent rawEvent)
        {
            var metadata = _serializer.DeserializeMetadata(rawEvent.Metadata);

            // try loading the type from the registry
            var clrType = _eventRegistry.GetClrType(rawEvent.EventType);

            // if it fails, try loading the metadata
            if (clrType == null && metadata != null)
            {
                object clrTypeName;

                if (metadata.TryGetValue("$CLRType", out clrTypeName))
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
                rawEvent.StreamID,
                rawEvent.StreamSequence,
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

        // base class for query and aggregate readers
        class WorkerBase : ReceiveActor
        {
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
                    Become(ReplayStarted);

                    try
                    {
                        handler(request);
                    }
                    catch (Exception ex)
                    {
                        Sender.Tell(new ReplayAborted { ReplayID = ReplayID, Exception = ex });
                    }

                    Context.Stop(Self);
                });
            }

            protected void ReplayStarted()
            {
                Receive<CancelReplayRequest>(request =>
                {
                    _cts.Cancel();
                    Sender.Tell(new ReplayCancelled { ReplayID = ReplayID });
                    Context.Stop(Self);

                }, request => request.ReplayID == ReplayID);

                Receive<WorkerFailure>(wf =>
                {
                    Context.GetLogger().Error(wf.Exception, "Unexpected Exception on " + this.GetType().Name);
                    _cts.Cancel();
                    Sender.Tell(new ReplayAborted { ReplayID = ReplayID, Exception = wf.Exception });
                    Context.Stop(Self);

                }, _ => Self.Equals(Sender));
            }
        }

        /// <summary>
        /// Handles replay requests from aggregates.
        /// </summary>
        class AggregateReplayWorker : WorkerBase
        {
            public AggregateReplayWorker(IStreamStoreReader reader, Func<IPersistedRawEvent, IPersistedEvent> deserializer)
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

                        await reader.ReadStreamAsync(request.StreamID, request.InitialSequence, Int32.MaxValue, e =>
                        {
                            if (IsReplayCancelled)
                                return;

                            globalSequence = e.GlobalSequence;

                            var @event = deserializer(e);

                            sender.Tell(new ReplayEvent
                            {
                                ReplayID = request.ReplayID,
                                Event = @event
                            }, self);

                        }, ReplayCancelToken);

                        sender.Tell(new ReplayCompleted { ReplayID = ReplayID, LastSeenGlobalSequence = globalSequence }, self);

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
            public ProjectionStreamReplayWorker(IStreamStoreReader reader, Func<IPersistedRawEvent, IPersistedEvent> deserializer)
            {
                ReceiveReplayRequest<ProjectionStreamReplayRequest>(request =>
                {
                    var sender = Sender;
                    var self = Self;

                    Task.Run(async () =>
                    {
                        if (IsReplayCancelled)
                            return;

                        var projectionStreamCheckpoint = 0L;
                        var indexedGlobalSequence = 0L;
                        var projectionStreamSequence = 0;

                        // check if the store supports projection indexes
                        var projectionStore = reader as IProjectionStoreReader;

                        if (projectionStore != null)
                        {
                            // grab whatever is the checkpoint for the projection
                            projectionStreamCheckpoint = await projectionStore.ReadProjectionCheckpointAsync(request.ProjectionID);

                            // if we need to send indexed events, read the events
                            if (request.SendIndexedEvents)
                            {
                                await projectionStore.ReadIndexedProjectionStreamAsync(request.ProjectionID, request.InitialSequence, request.MaxEvents, e =>
                                {
                                    if (IsReplayCancelled)
                                        return;

                                    indexedGlobalSequence = e.GlobalSequence;
                                    projectionStreamSequence = e.ProjectionStreamSequence;

                                    var persistedEvent = deserializer(e);
                                    var projectionEvent = ProjectionEventFactory.Create(e.ProjectionStreamID, e.ProjectionStreamSequence, persistedEvent);

                                    var replayEvent = new ProjectionReplayEvent
                                    {
                                        ReplayID = request.ReplayID,
                                        Event = projectionEvent
                                    };

                                    sender.Tell(replayEvent, self);

                                }, ReplayCancelToken);
                            }
                            // otherwise we need to find out at least the highest sequence
                            else
                            {
                                projectionStreamSequence = await projectionStore.ReadHighestProjectionStreamSequenceAsync(request.ProjectionID);
                            }
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
                            LastSeenProjectionStreamSequence = projectionStreamSequence
                        }, self);

                        // try reading additional events from the global event stream that weren't emitted yet 
                        var maxEvents = request.MaxEvents == Int32.MaxValue ? request.MaxEvents : request.MaxEvents - projectionStreamSequence;

                        if (maxEvents > 0)
                        {
                            await reader.ReadAsync(globalSequence + 1, maxEvents, e =>
                            {
                                if (IsReplayCancelled)
                                    return;

                                globalSequence = e.GlobalSequence;

                                var @event = deserializer(e);

                                sender.Tell(new ReplayEvent
                                {
                                    ReplayID = request.ReplayID,
                                    Event = @event

                                }, self);

                            }, ReplayCancelToken);
                        }

                        sender.Tell(new ReplayCompleted { ReplayID = ReplayID, LastSeenGlobalSequence = globalSequence }, self);

                    })
                    .ContinueWith(t =>
                    {
                        self.Tell(new WorkerFailure { Exception = t.Exception }, self);

                    }, TaskContinuationOptions.NotOnRanToCompletion);
                });
            }
        }
    }
}
