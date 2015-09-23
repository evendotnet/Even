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

        public void ReceiveRequest<TRequest>(Func<TRequest, CancellationToken, Task> action) where TRequest : IRequest
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
                        await action(req, _cts.Token);

                        if (!_cts.IsCancellationRequested)
                            SendFinishedMessage(sender, requestId);
                    }
                    catch (Exception ex)
                    {
                        sender.Tell(new ReadAborted(requestId, ex), ActorRefs.NoSender);
                    }

                    self.Tell(PoisonPill.Instance);
                });

                Become(() =>
                {
                    Receive<CancelReadRequest>(_ =>
                    {
                        _cts.Cancel();
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
        public ReadWorker(IEventStoreReader reader, PersistedEventFactory2 factory)
        {
            ReceiveRequest<ReadRequest>((req, ct) =>
            {
                var sender = Sender;

                return reader.ReadAsync(req.InitialGlobalSequence, req.Count, e =>
                {
                    var loadedEvent = factory.CreateEvent(e);
                    sender.Tell(new ReadEventResponse(req.RequestID, loadedEvent), ActorRefs.NoSender);
                }, ct);
            });
        }
    }

    internal class ReadStreamWorker : ReadWorkerBase
    {
        public ReadStreamWorker(IEventStoreReader reader, PersistedEventFactory2 factory)
        {
            ReceiveRequest<ReadStreamRequest>((req, ct) =>
            {
                var sender = Sender;

                var sequence = req.InitialSequence;

                return reader.ReadStreamAsync(req.StreamID, req.InitialSequence, req.Count, e =>
                {
                    var loadedEvent = factory.CreateStreamEvent(e, sequence++);
                    sender.Tell(new ReadEventStreamResponse(req.RequestID, loadedEvent), ActorRefs.NoSender);
                }, ct);
            });
        }
    }

    internal class ReadIndexedProjectionStreamWorker : ReadWorkerBase
    {
        long _lastSeenGlobalCheckpoint;

        public ReadIndexedProjectionStreamWorker(IProjectionStoreReader reader, PersistedEventFactory2 factory)
        {
            ReceiveRequest<ReadIndexedProjectionRequest>(async (req, ct) =>
            {
                var sender = Sender;

                var checkpoint = await reader.ReadProjectionCheckpointAsync(req.ProjectionStreamID);
                var sequence = 0L;

                await reader.ReadIndexedProjectionStreamAsync(req.ProjectionStreamID, req.InitialSequence, req.Count, e =>
                {
                    sequence = e.GlobalSequence;
                    var loadedEvent = factory.CreateEvent(e);
                    sender.Tell(new ReadEventResponse(req.RequestID, loadedEvent), ActorRefs.NoSender);
                }, ct);

                _lastSeenGlobalCheckpoint = Math.Max(checkpoint, sequence);
            });
        }

        protected override void SendFinishedMessage(IActorRef sender, Guid requestId)
        {
            sender.Tell(new ReadProjectionIndexFinished(requestId, _lastSeenGlobalCheckpoint), ActorRefs.NoSender);
        }
    }
}
