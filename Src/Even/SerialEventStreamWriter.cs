using Akka.Actor;
using Even.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class SerialEventStreamWriter : ReceiveActor
    {
        IEventStoreWriter _writer;
        ISerializer _serializer;
        IActorRef _dispatcher;

        public SerialEventStreamWriter(IEventStoreWriter writer, ISerializer serializer, IActorRef dispatcher)
        {
            Argument.Requires(writer != null, nameof(writer));
            Argument.Requires(serializer != null, nameof(serializer));
            Argument.Requires(dispatcher != null, nameof(dispatcher));

            this._writer = writer;
            this._serializer = serializer;
            this._dispatcher = dispatcher;

            Receive<PersistenceRequest>(r => HandleRequest(r));
        }

        private async Task HandleRequest(PersistenceRequest request)
        {
            try
            {
                await WriteEvents(request);
                Sender.Tell(new PersistenceSuccess(request.PersistenceID));
            }
            catch (UnexpectedStreamSequenceException)
            {
                Sender.Tell(new UnexpectedStreamSequence(request.PersistenceID));
            }
            catch (DuplicatedEntryException)
            {
                Sender.Tell(new DuplicatedEntry(request.PersistenceID));
            }
            catch (Exception ex)
            {
                Sender.Tell(new PersistenceFailure(request.PersistenceID, ex));
            }
        }

        protected async Task WriteEvents(PersistenceRequest request)
        {
            var events = request.Events;

            // serialize the events into raw events
            var rawEvents = UnpersistedRawEvent.FromUnpersistedEvents(events, _serializer);

            // writes all events to the store
            await _writer.WriteStreamAsync(request.StreamID, request.ExpectedStreamSequence, rawEvents);

            // publishes the events in the order they were sent

            var setSequence = request.ExpectedStreamSequence != ExpectedSequence.Any;
            var sequence = request.ExpectedStreamSequence + 1;
            var i = 0;

            foreach (var e in events)
            {
                var rawEvent = rawEvents[i++];
                var persistedEvent = PersistedEventFactory.Create(rawEvent.GlobalSequence, setSequence ? sequence++ : -1, e);

                // notify the sender
                Sender.Tell(persistedEvent);

                // publish to the event stream
                Context.System.EventStream.Publish(persistedEvent);
            }
        }
    }
}
