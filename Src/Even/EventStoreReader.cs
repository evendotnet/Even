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
            Receive<ReplayAggregateRequest>(request =>
            {
                Log.Debug("Received ReplayAggregateRequest");
                var props = PropsFactory.Create<AggregateReplayWorker>(_readerFactory(), _serializer, _cryptoService);
                var actor = Context.ActorOf(props);
                actor.Forward(request);
            });

            Receive<ReplayQueryRequest>(request =>
            {
                Log.Debug("Received ReplayQueryRequest");
                var props = PropsFactory.Create<QueryReplayWorker>(_readerFactory(), _serializer, _cryptoService);
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
                Become(OnStartReplay);
            }

            protected virtual void OnStartReplay()
            {
                Receive<ReplayCancelRequest>(request =>
                {
                    _cts.Cancel();
                    Sender.Tell(new ReplayCancelled { ReplayID = ReplayID });
                }, request => request.ReplayID == ReplayID);
            }

            protected IStreamEvent DeserializeEvent(IRawStorageEvent rawEvent)
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

            protected IAggregateSnapshot DeserializeSnapshot(IRawAggregateSnapshot rawSnapshot)
            {
                throw new NotImplementedException();
            }
        }

        class AggregateReplayWorker : WorkerBase
        {
            CancellationTokenSource _cts = new CancellationTokenSource();

            public AggregateReplayWorker(IStorageReader reader, IDataSerializer serializer, ICryptoService cryptoService)
                : base(reader, serializer, cryptoService)
            {
                Receive<ReplayAggregateRequest>(request =>
                {
                    StartReplay(request.ReplayID);

                    var sender = Sender;
                    var self = Self;

                    Task.Run(async () =>
                    {
                        if (IsReplayCancelled)
                            return;

                        var rawSnapshot = await reader.ReadAggregateSnapshotAsync(request.StreamID, ReplayCancelToken);

                        if (rawSnapshot != null)
                        {
                            var snapshot = DeserializeSnapshot(rawSnapshot);

                            sender.Tell(new AggregateReplaySnapshot
                            {
                                ReplayID = request.ReplayID,
                                Snapshot = snapshot
                            });
                        }

                        if (IsReplayCancelled)
                            return;

                        var initialSequence = rawSnapshot?.StreamSequence ?? 1;

                        await reader.ReadAggregateStreamAsync(request.StreamID, initialSequence, Int32.MaxValue, e =>
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

                    }).ContinueWith(t => {

                        sender.Tell(new ReplayAborted {
                            ReplayID = ReplayID,
                            Exception = t.Exception.InnerException
                        });

                    }, TaskContinuationOptions.OnlyOnFaulted);
                });
            }
        }

        class QueryReplayWorker : WorkerBase
        {
            bool _stopRequested;

            public QueryReplayWorker(IStorageReader reader, IDataSerializer serializer, ICryptoService cryptoService)
                : base(reader, serializer, cryptoService)
            {
                Receive<ReplayQueryRequest>(request =>
                {
                    StartReplay(request.ReplayID);

                    var sender = Sender;
                    var self = Self;

                    Task.Run(async () =>
                    {
                        // then start recovering the events
                        await reader.QueryEventsAsync(request.Query, request.InitialCheckpoint, request.MaxEvents, e => {

                            if (_stopRequested)
                                return false;

                            var streamEvent = DeserializeEvent(e);

                            sender.Tell(new ReplayEvent
                            {
                                ReplayID = request.ReplayID,
                                Event = streamEvent
                            });

                            if (_stopRequested)
                                return false;

                            return true;
                        }, ReplayCancelToken);

                        // notify the recovery was completed
                        sender.Tell(new ReplayCompleted { ReplayID = request.ReplayID }, self);
                    }).ContinueWith(t => {

                        sender.Tell(new ReplayAborted
                        {
                            ReplayID = ReplayID,
                            Exception = t.Exception.InnerException
                        });

                    }, TaskContinuationOptions.OnlyOnFaulted);
                });
            }

            protected override void OnStartReplay()
            {
                base.OnStartReplay();

                Receive<ReplayStopRequest>(r =>
                {
                    _stopRequested = true;
                }, r => r.ReplayID == ReplayID);
            }
        }
    }
}
