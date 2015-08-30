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
        public EventStoreReader()
        {
            Receive<InitializeEventStoreReader>(msg =>
            {
                _readerFactory = msg.ReaderFactory;
                _serializer = msg.Serializer;
                _cryptoService = msg.CryptoService;

                Become(ReceiveRequests);
            });
        }

        Func<IStorageReader> _readerFactory;
        IDataSerializer _serializer;
        ICryptoService _cryptoService;
        ILoggingAdapter Log = Context.GetLogger();

        public void ReceiveRequests()
        {
            Receive<ReplayStreamRequest>(request =>
            {
                Log.Debug("Received ReplayAggregateRequest");
                var props = PropsFactory.Create<StreamReplayWorker>(_readerFactory(), _serializer, _cryptoService);
                var actor = Context.ActorOf(props);
                actor.Forward(request);
            });

            Receive<ReplayEventsRequest>(request =>
            {
                Log.Debug("Received ReplayQueryRequest");
                var props = PropsFactory.Create<EventReplayWorker>(_readerFactory(), _serializer, _cryptoService);
                var actor = Context.ActorOf(props);
                actor.Forward(request);
            });

            Receive<ProjectionStreamReplayRequest>(request =>
            {
                Log.Debug("Received ReplayProjectionStreamRequest");

                var props = PropsFactory.Create<ProjectionStreamReplayWorker>(_readerFactory(), _serializer, _cryptoService);
                var actor = Context.ActorOf(props);
                actor.Forward(request);
            });
        }

        // base class for query and aggregate readers
        class WorkerBase : ReceiveActor
        {
            public WorkerBase(IStorageReader reader, IDataSerializer serializer, ICryptoService cryptoService)
            {
                Reader = reader;
                Serializer = serializer;
                CryptoService = cryptoService;
            }

            // cancellation token source used to signal cancellation requests
            private CancellationTokenSource _cts = new CancellationTokenSource();

            protected Guid ReplayID { get; private set; }
            protected IStorageReader Reader { get; }
            protected IDataSerializer Serializer { get; }
            protected ICryptoService CryptoService { get; }
            protected CancellationToken ReplayCancelToken => _cts.Token;
            protected bool IsReplayCancelled => _cts.IsCancellationRequested;
            protected ILoggingAdapter Log = Context.GetLogger();

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
                Receive<ReplayCancelRequest>(request =>
                {
                    _cts.Cancel();
                    Sender.Tell(new ReplayCancelled { ReplayID = ReplayID });
                }, request => request.ReplayID == ReplayID);
            }

            protected IEvent DeserializeEvent(IRawStorageEvent rawEvent)
            {
                var headerBytes = rawEvent.Headers;
                var headers = Serializer.Deserialize(headerBytes);

                var payloadBytes = rawEvent.Payload;

                if (CryptoService != null)
                    payloadBytes = CryptoService.Decrypt(payloadBytes);

                object clrType;
                object domainEvent = null;

                if (headers.TryGetValue("CLRType", out clrType))
                {
                    try
                    {
                        var type = Type.GetType(clrType as string);
                        domainEvent = Serializer.Deserialize(type, payloadBytes);
                    }
                    catch { }
                }

                if (domainEvent == null)
                {
                    Context.GetLogger().Debug("Can't deserialize event to type '{0}' on checkpoint '{1}', falling back to dictionary.", clrType, rawEvent.Checkpoint);
                    domainEvent = Serializer.Deserialize(payloadBytes);
                }

                return new PersistedStreamEvent(rawEvent.Checkpoint, rawEvent.EventID, rawEvent.StreamID, rawEvent.StreamSequence, rawEvent.EventName, headers, domainEvent);
            }

            protected IStreamSnapshot DeserializeSnapshot(IRawAggregateSnapshot rawSnapshot)
            {
                throw new NotImplementedException();
            }
        }

        class StreamReplayWorker : WorkerBase
        {
            CancellationTokenSource _cts = new CancellationTokenSource();

            public StreamReplayWorker(IStorageReader reader, IDataSerializer serializer, ICryptoService cryptoService)
                : base(reader, serializer, cryptoService)
            {
                Receive<ReplayStreamRequest>(request =>
                {
                    StartReplay(request.ReplayID);

                    var sender = Sender;
                    var self = Self;

                    Task.Run(async () =>
                    {
                        if (IsReplayCancelled)
                            return;

                        var rawSnapshot = await reader.ReadStreamSnapshotAsync(request.StreamID, ReplayCancelToken);

                        if (rawSnapshot != null)
                        {
                            var snapshot = DeserializeSnapshot(rawSnapshot);

                            sender.Tell(new ReplayStreamSnapshot
                            {
                                ReplayID = request.ReplayID,
                                Snapshot = snapshot
                            });
                        }

                        if (IsReplayCancelled)
                            return;

                        var initialSequence = rawSnapshot?.StreamSequence ?? 1;

                        await reader.ReadStreamAsync(request.StreamID, initialSequence, Int32.MaxValue, e =>
                        {
                            if (IsReplayCancelled)
                                return;

                            var streamEvent = DeserializeEvent(e);

                            sender.Tell(new ReplayEvent
                            {
                                ReplayID = request.ReplayID,
                                Event = streamEvent
                            });

                        }, ReplayCancelToken);

                        if (!IsReplayCancelled)
                            sender.Tell(new ReplayCompleted { ReplayID = request.ReplayID });

                    }).ContinueWith(t =>
                    {

                        sender.Tell(new ReplayAborted
                        {
                            ReplayID = ReplayID,
                            Exception = t.Exception.InnerException
                        });

                    }, TaskContinuationOptions.OnlyOnFaulted);
                });
            }
        }

        class EventReplayWorker : WorkerBase
        {
            public EventReplayWorker(IStorageReader reader, IDataSerializer serializer, ICryptoService cryptoService)
                : base(reader, serializer, cryptoService)
            {
                Receive<ReplayEventsRequest>(request =>
                {
                    StartReplay(request.ReplayID);

                    var sender = Sender;
                    var self = Self;

                    Task.Run(async () =>
                    {
                        long checkpoint = 0;

                        await reader.ReadAsync(request.InitialCheckpoint, request.MaxEvents, e =>
                        {
                            var streamEvent = DeserializeEvent(e);

                            sender.Tell(new ReplayEvent
                            {
                                ReplayID = request.ReplayID,
                                Event = streamEvent
                            });

                            checkpoint = e.Checkpoint;

                        }, ReplayCancelToken);

                        // notify the recovery was completed
                        sender.Tell(new ReplayCompleted { ReplayID = request.ReplayID }, self);
                    }).ContinueWith(t =>
                    {

                        sender.Tell(new ReplayAborted
                        {
                            ReplayID = ReplayID,
                            Exception = t.Exception.InnerException
                        });

                    }, TaskContinuationOptions.OnlyOnFaulted);
                });
            }
        }

        class ProjectionStreamReplayWorker : WorkerBase
        {
            public ProjectionStreamReplayWorker(IStorageReader reader, IDataSerializer serializer, ICryptoService cryptoService)
                : base(reader, serializer, cryptoService)
            {
                Receive<ProjectionStreamReplayRequest>(request =>
                {
                    StartReplay(request.ReplayID);

                    var sender = Sender;
                    var self = Self;

                    Task.Run(async () =>
                    {
                        var sequence = 0;
                        var checkpoint = 0l;

                        if (IsReplayCancelled)
                            return;

                        // if we need to send indexed events, read the events
                        if (request.SendIndexedEvents)
                        {
                            await reader.ReadProjectionEventStreamAsync(request.ProjectionID, request.InitialSequence, request.MaxEvents, e =>
                            {
                                checkpoint = e.Checkpoint;
                                sequence = e.ProjectionSequence;

                                var streamEvent = DeserializeEvent(e);

                                var replayEvent = new ProjectionReplayEvent
                                {
                                    ReplayID = request.ReplayID,
                                    Event = new ProjectionEvent(e.ProjectionID, e.ProjectionSequence, streamEvent)
                                };

                                sender.Tell(replayEvent);

                            }, ReplayCancelToken);
                        }
                        // otherwise we need to find out at least the highest sequence
                        else
                        {
                            sequence = await reader.GetHighestProjectionStreamSequenceAsync(request.ProjectionID);
                        }

                        var storedCheckpoint = await reader.GetHighestProjectionCheckpoint(request.ProjectionID);

                        // ensure we know the last checkpoing if the index is far from the actually checked checkpoint
                        checkpoint = Math.Max(storedCheckpoint, checkpoint);

                        // sinals the end of the index and the start of the non-indexed event stream
                        sender.Tell(new ProjectionStreamIndexReplayCompleted
                        {
                            ReplayID = request.ReplayID,
                            LastSeenCheckpoint = checkpoint,
                            LastSeenSequence = sequence
                        }, self);

                        if (IsReplayCancelled)
                            return;

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

                        sender.Tell(new ReplayCompleted { ReplayID = request.ReplayID, LastCheckpoint = checkpoint }, self);
                    }).ContinueWith(t =>
                    {
                        sender.Tell(new ReplayAborted { ReplayID = request.ReplayID, Exception = t.Exception });

                    }, TaskContinuationOptions.NotOnRanToCompletion);
                });
            }
        }
    }
}
