using Akka.Actor;
using Even.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Even
{
    internal class ReadWorkerBase : ReceiveActor
    {
        CancellationTokenSource _cts = new CancellationTokenSource();

        public void ReceiveRequest<TRequest>(Func<TRequest, IActorRef, CancellationToken, Task> action) where TRequest : IRequest
        {
            Receive<TRequest>(req =>
            {
                var requestId = req.RequestID;
                var sender = Sender;
                var self = Self;

                Task.Run(async () =>
                {
                    try
                    {
                        await action(req, sender, _cts.Token);

                        if (!_cts.IsCancellationRequested)
                            SendFinishedMessage(sender, requestId);
                    }
                    catch (Exception ex)
                    {
                        sender.Tell(new Aborted(requestId, ex), ActorRefs.NoSender);
                    }

                    self.Tell(PoisonPill.Instance);
                });

                Become(() =>
                {
                    Receive<CancelRequest>(_ =>
                    {
                        _cts.Cancel();
                        sender.Tell(new Cancelled(req.RequestID));
                    }, msg => msg.RequestID == requestId);
                });
            });
        }

        protected virtual void SendFinishedMessage(IActorRef sender, Guid requestId)
        {
            sender.Tell(new ReadFinished(requestId), ActorRefs.NoSender);
        }
    }

    internal class ReadWorker : ReadWorkerBase
    {
        public static Props CreateProps(IEventStoreReader reader, IPersistedEventFactory factory)
        {
            Argument.RequiresNotNull(reader, nameof(reader));
            Argument.RequiresNotNull(factory, nameof(factory));

            return Props.Create<ReadWorker>(reader, factory);
        }

        public ReadWorker(IEventStoreReader reader, IPersistedEventFactory factory)
        {
            ReceiveRequest<ReadRequest>((req, sender, ct) =>
            {
                return reader.ReadAsync(req.InitialGlobalSequence, req.Count, e =>
                {
                    var loadedEvent = factory.CreateEvent(e);
                    sender.Tell(new ReadResponse(req.RequestID, loadedEvent), ActorRefs.NoSender);
                }, ct);
            });
        }
    }

    internal class ReadStreamWorker : ReadWorkerBase
    {
        public static Props CreateProps(IEventStoreReader reader, IPersistedEventFactory factory)
        {
            Argument.RequiresNotNull(reader, nameof(reader));
            Argument.RequiresNotNull(factory, nameof(factory));

            return Props.Create<ReadStreamWorker>(reader, factory);
        }

        public ReadStreamWorker(IEventStoreReader reader, IPersistedEventFactory factory)
        {
            ReceiveRequest<ReadStreamRequest>((req, sender, ct) =>
            {
                var sequence = req.InitialSequence;

                return reader.ReadStreamAsync(req.StreamID, req.InitialSequence, req.Count, e =>
                {
                    var loadedEvent = factory.CreateStreamEvent(e, sequence++);
                    sender.Tell(new ReadStreamResponse(req.RequestID, loadedEvent), ActorRefs.NoSender);
                }, ct);
            });
        }

        protected override void SendFinishedMessage(IActorRef sender, Guid requestId)
        {
            sender.Tell(new ReadStreamFinished(requestId), ActorRefs.NoSender);
        }
    }

    internal class ReadIndexedProjectionStreamWorker : ReadWorkerBase
    {
        long _lastSeenGlobalCheckpoint;

        public static Props CreateProps(IEventStoreReader reader, IPersistedEventFactory factory)
        {
            Argument.RequiresNotNull(reader, nameof(reader));
            Argument.RequiresNotNull(factory, nameof(factory));

            return Props.Create<ReadIndexedProjectionStreamWorker>(reader, factory);
        }

        public ReadIndexedProjectionStreamWorker(IProjectionStoreReader reader, IPersistedEventFactory factory)
        {
            ReceiveRequest<ReadIndexedProjectionStreamRequest>(async (req, sender, ct) =>
            {
                var checkpoint = await reader.ReadProjectionCheckpointAsync(req.ProjectionStreamID);
                var sequence = req.InitialSequence;
                var globalSequence = 0L;

                await reader.ReadIndexedProjectionStreamAsync(req.ProjectionStreamID, req.InitialSequence, req.Count, e =>
                {
                    globalSequence = e.GlobalSequence;
                    var loadedEvent = factory.CreateStreamEvent(e, sequence++);
                    sender.Tell(new ReadIndexedProjectionStreamResponse(req.RequestID, loadedEvent), ActorRefs.NoSender);
                }, ct);

                _lastSeenGlobalCheckpoint = Math.Max(checkpoint, globalSequence);
            });
        }

        protected override void SendFinishedMessage(IActorRef sender, Guid requestId)
        {
            sender.Tell(new ReadIndexedProjectionStreamFinished(requestId, _lastSeenGlobalCheckpoint), ActorRefs.NoSender);
        }
    }

    internal class ReadHighestGlobalSequenceWorker : ReceiveActor
    {
        public static Props CreateProps(IEventStoreReader reader)
        {
            Argument.RequiresNotNull(reader, nameof(reader));

            return Props.Create<ReadHighestGlobalSequenceWorker>(reader);
        }

        public ReadHighestGlobalSequenceWorker(IEventStoreReader reader)
        {
            Receive<ReadHighestGlobalSequenceRequest>(async r =>
            {
                try
                {
                    var globalSequence = await reader.ReadHighestGlobalSequenceAsync();
                    Sender.Tell(new ReadHighestGlobalSequenceResponse(r.RequestID, globalSequence));
                }
                finally
                {
                    Context.Stop(Self);
                }
            });
        }
    }
}
