using Akka.Actor;
using Akka.Event;
using Even.Messages;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Even
{
    public class EventStoreReader : ReceiveActor
    {
        IStreamStoreReader _storeReader;
        IDataSerializer _serializer;
        ICryptoService _cryptoService;
        ILoggingAdapter _log = Context.GetLogger();

        public EventStoreReader()
        {
            Receive<InitializeEventStoreReader>(ini =>
            {
                _storeReader = ini.StoreReader;
                _serializer = ini.Serializer;
                _cryptoService = ini.CryptoService;

                Become(Ready);
            });
        }

        void Ready()
        {
            Receive<ReplayAggregateRequest>(request =>
            {
                _log.Debug("Received ReplayAggregateRequest");
                var props = PropsFactory.Create<AggregateReplayWorker>(_storeReader, _serializer, _cryptoService);
                var actor = Context.ActorOf(props);
                actor.Forward(request);
            });

            Receive<ProjectionStreamReplayRequest>(request =>
            {
                _log.Debug("Received ReplayProjectionStreamRequest");

                var props = PropsFactory.Create<ProjectionStreamReplayWorker>(_storeReader, _serializer, _cryptoService);
                var actor = Context.ActorOf(props);
                actor.Forward(request);
            });
        }

        // base class for query and aggregate readers
        class WorkerBase : ReceiveActor
        {
            protected Guid ReplayID { get; private set; }
            protected IDataSerializer Serializer { get; }
            protected ICryptoService CryptoService { get; }
            protected ILoggingAdapter Log { get; } = Context.GetLogger();

            protected CancellationToken ReplayCancelToken => _cts.Token;
            protected bool IsReplayCancelled => _cts.IsCancellationRequested;

            // cancellation token source used to signal cancellation requests
            private CancellationTokenSource _cts = new CancellationTokenSource();

            public WorkerBase(IDataSerializer serializer, ICryptoService cryptoService)
            {
                Serializer = serializer;
                CryptoService = cryptoService;
            }

            /// <summary>
            /// Signals the actor it started the replay. 
            /// Only a single replay can be requested per actor, and once it started it only receives cancellation requests.
            /// </summary>
            protected void StartReplay(Guid replayId)
            {
                ReplayID = replayId;
                Become(ReplayStarted);
            }

            protected void ReplayStarted()
            {
                Receive<CancelReplayRequest>(request =>
                {
                    _cts.Cancel();
                    Sender.Tell(new ReplayCancelled { ReplayID = ReplayID });
                    Context.Stop(Self);

                }, request => request.ReplayID == ReplayID);

                Receive<Exception>(ex =>
                {
                    _cts.Cancel();
                    Sender.Tell(new ReplayAborted { ReplayID = ReplayID, Exception = ex });
                    Context.Stop(Self);

                });
            }

            protected IPersistedEvent DeserializeEvent(IRawPersistedEvent e)
            {
                var headers = Serializer.Deserialize(e.Headers);

                var payloadBytes = e.Payload;

                if (CryptoService != null)
                    payloadBytes = CryptoService.Decrypt(payloadBytes);

                var typeName = headers["CLRType"] as string;
                var type = Type.GetType(typeName);
                var domainEvent = Serializer.Deserialize(type, payloadBytes);

                return EventFactory.CreatePersistedEvent(e, domainEvent);
            }

            protected IAggregateSnapshot DeserializeSnapshot(IRawAggregateSnapshot rawSnapshot)
            {
                var payloadBytes = rawSnapshot.Payload;

                if (CryptoService != null)
                    payloadBytes = CryptoService.Decrypt(payloadBytes);

                var type = Type.GetType(rawSnapshot.ClrType);
                var state = Serializer.Deserialize(type, payloadBytes);

                return new AggregateSnapshot
                {
                    StreamID = rawSnapshot.StreamID,
                    StreamSequence = rawSnapshot.StreamSequence,
                    State = state
                };
            }
        }

        /// <summary>
        /// Handles replay requests from aggregates.
        /// </summary>
        class AggregateReplayWorker : WorkerBase
        {
            CancellationTokenSource _cts = new CancellationTokenSource();

            public AggregateReplayWorker(IStreamStoreReader reader, IDataSerializer serializer, ICryptoService cryptoService)
                : base(serializer, cryptoService)
            {
                Receive<ReplayAggregateRequest>(request =>
                {
                    StartReplay(request.ReplayID);

                    var sender = Sender;
                    var self = Self;

                    Task.Run(async() =>
                    {
                        if (IsReplayCancelled)
                            return;

                        var initialSequence = request.InitialSequence;
                        var snapshotSent = false;

                        // only read the snapshot if requested and reading from start
                        if (request.UseSnapshot && initialSequence <= 1)
                        {
                            // if the store supports snapshots, try to read from it
                            var snapshotStore = reader as IAggregateSnapshotStore;

                            if (snapshotStore != null)
                            {
                                var rawSnapshot = await snapshotStore.ReadAggregateSnapshotAsync(request.StreamID);

                                if (rawSnapshot != null)
                                {
                                    IAggregateSnapshot snapshot;

                                    try
                                    {
                                        snapshot = DeserializeSnapshot(rawSnapshot);
                                    }
                                    catch (Exception ex)
                                    {
                                        // if any error happen during snapshot deserialization, we ignore the snapshot
                                        snapshot = null;
                                        Log.Error(ex, "Error deserializing snapshot");
                                    }

                                    // if the snapshot was found
                                    if (snapshot != null)
                                    {
                                        // update the initial sequence to the one after the snapshot
                                        initialSequence = snapshot.StreamSequence + 1;

                                        // send to the aggregate
                                        sender.Tell(new AggregateSnapshotOffer
                                        {
                                            ReplayID = request.ReplayID,
                                            Snapshot = snapshot
                                        }, self);

                                        snapshotSent = true;
                                    }
                                }
                            }
                        }

                        if (!snapshotSent)
                            sender.Tell(new NoAggregateSnapshotOffer { ReplayID = ReplayID }, self);

                        // continue reading from events

                        var isAborted = false;
                        
                        await reader.ReadStreamAsync(request.StreamID, initialSequence, Int32.MaxValue, e =>
                        {
                            if (IsReplayCancelled || isAborted)
                                return;

                            IPersistedEvent @event;

                            try
                            {
                                @event = DeserializeEvent(e);
                            }
                            catch (Exception ex)
                            {
                                self.Tell(ex, self);
                                isAborted = true;
                                return;
                            }

                            sender.Tell(new ReplayEvent
                            {
                                ReplayID = request.ReplayID,
                                Event = @event
                            }, self);

                        }, ReplayCancelToken);

                        if (!IsReplayCancelled)
                        {
                            sender.Tell(new ReplayCompleted
                            {
                                ReplayID = request.ReplayID
                            }, self);
                        }
                    })
                    .ContinueWith(t =>
                    {
                        sender.Tell(new ReplayAborted
                        {
                            ReplayID = ReplayID,
                            Exception = t.Exception.InnerException
                        }, self);

                    }, TaskContinuationOptions.NotOnRanToCompletion);
                });
            }
        }

        /// <summary>
        /// Handles replay requests for projection streams
        /// </summary>
        //class EventReplayWorker : WorkerBase
        //{
        //    public EventReplayWorker(IStorageReader reader, IDataSerializer serializer, ICryptoService cryptoService)
        //        : base(serializer, cryptoService)
        //    {
        //        Receive<EventReplayRequest>(request =>
        //        {
        //            StartReplay(request.ReplayID);

        //            var sender = Sender;
        //            var self = Self;

        //            Task.Run(async () =>
        //            {
        //                long checkpoint = 0;

        //                await reader.ReadAsync(request.InitialCheckpoint, request.MaxEvents, e =>
        //                {
        //                    var streamEvent = DeserializeEvent(e);

        //                    sender.Tell(new ReplayEvent
        //                    {
        //                        ReplayID = request.ReplayID,
        //                        Event = streamEvent
        //                    });

        //                    checkpoint = e.Checkpoint;

        //                }, ReplayCancelToken);

        //                // notify the recovery was completed
        //                sender.Tell(new ReplayCompleted { ReplayID = request.ReplayID }, self);
        //            })
        //            .ContinueWith(t =>
        //            {

        //                sender.Tell(new ReplayAborted
        //                {
        //                    ReplayID = ReplayID,
        //                    Exception = t.Exception.InnerException
        //                });

        //            }, TaskContinuationOptions.OnlyOnFaulted);
        //        });
        //    }
        //}

        class ProjectionStreamReplayWorker : WorkerBase
        {
            public ProjectionStreamReplayWorker(IStreamStoreReader reader, IDataSerializer serializer, ICryptoService cryptoService)
                : base(serializer, cryptoService)
            {
                Receive<ProjectionStreamReplayRequest>(request =>
                {
                    StartReplay(request.ReplayID);

                    var sender = Sender;
                    var self = Self;

                    Task.Run(async () =>
                    {
                        if (IsReplayCancelled)
                            return;

                        var sequence = request.InitialSequence;
                        var checkpoint = 0L;

                        // check if the store supports projection indexes
                        var projectionStore = reader as IProjectionStore;

                        if (projectionStore != null)
                        {
                            // if we need to send indexed events, read the events
                            if (request.SendIndexedEvents)
                            {
                                var isAborted = false;

                                await projectionStore.ReadProjectionEventStreamAsync(request.ProjectionID, request.InitialSequence, request.MaxEvents, e =>
                                {
                                    if (IsReplayCancelled || isAborted)
                                        return;

                                    checkpoint = e.Checkpoint;
                                    sequence = e.ProjectionSequence;

                                    IPersistedEvent @event;

                                    try
                                    {
                                        @event = DeserializeEvent(e);
                                    }
                                    catch (Exception ex)
                                    {
                                        self.Tell(ex, self);
                                        isAborted = true;
                                        return;
                                    }

                                    var replayEvent = new ProjectionReplayEvent
                                    {
                                        ReplayID = request.ReplayID,
                                        Event = EventFactory.CreateProjectionEvent(e.ProjectionStreamID, e.ProjectionSequence, @event)
                                    };

                                    sender.Tell(replayEvent, self);

                                }, ReplayCancelToken);
                            }
                            // otherwise we need to find out at least the highest sequence
                            else
                            {
                                sequence = await projectionStore.ReadHighestProjectionStreamSequenceAsync(request.ProjectionID);
                            }

                            var storedCheckpoint = await projectionStore.ReadHighestProjectionCheckpointAsync(request.ProjectionID);

                            // ensure we're seeing the last checkpoint the projection stream saw
                            checkpoint = Math.Max(storedCheckpoint, checkpoint);
                        }

                        if (IsReplayCancelled)
                            return;

                        // sinals the end of the index and the start of the non-indexed event stream
                        sender.Tell(new ProjectionStreamIndexReplayCompleted
                        {
                            ReplayID = request.ReplayID,
                            LastSeenCheckpoint = checkpoint,
                            LastSeenSequence = sequence
                        }, self);

                        // try reading additional events from the global event stream that weren't emitted yet 
                        var maxEvents = request.MaxEvents - sequence;

                        if (maxEvents > 0)
                        {
                            await reader.ReadAsync(checkpoint, maxEvents,  e =>
                            {
                                var streamEvent = DeserializeEvent(e);

                                sender.Tell(new ReplayEvent
                                {
                                    ReplayID = request.ReplayID,
                                    Event = streamEvent
                                }, self);

                                checkpoint = e.Checkpoint;

                            }, ReplayCancelToken);
                        }

                        if (!IsReplayCancelled)
                        {
                            sender.Tell(new ReplayCompleted
                            {
                                ReplayID = request.ReplayID,
                                LastCheckpoint = checkpoint
                            }, self);
                        }
                    }).ContinueWith(t =>
                    {
                        sender.Tell(new ReplayAborted
                        {
                            ReplayID = request.ReplayID,
                            Exception = t.Exception
                        }, self);

                    }, TaskContinuationOptions.NotOnRanToCompletion);
                });
            }
        }
    }
}
