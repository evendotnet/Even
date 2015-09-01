using Akka.Actor;
using Even.Messages;
using Even.Messages.Initialization;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class EventStoreSetup
    {
        private ActorSystem _system;
        private List<EventProcessorEntry> _eventProcessors = new List<EventProcessorEntry>();
        private EventStoreSettings _settings = new EventStoreSettings();

        public EventStoreSetup(ActorSystem system)
        {
            Contract.Requires(system != null);
            this._system = system;
        }

        public EventStoreSetup UseStore(IStreamStore store)
        {
            Contract.Requires(store != null);
            _settings.Store = store;
            return this;
        }

        public EventStoreSetup UseEncryption(ICryptoService cryptoService)
        {
            Contract.Requires(cryptoService != null);
            _settings.CryptoService = cryptoService;
            return this;
        }

        public EventStoreSetup UseSerializer(IDataSerializer serializer)
        {
            Contract.Requires(serializer != null);
            _settings.Serializer = serializer;

            return this;
        }

        public EventStoreSetup AddEventProcessor<T>(string name = null)
            where T : EventProcessor
        {
            _eventProcessors.Add(new EventProcessorEntry
            {
                Type = typeof(T),
                Name = name
            });

            return this;
        }

        public async Task<EventStoreGateway> Start()
        {
            var esRef = _system.ActorOf<EventStore>("EventStore");

            var state = await esRef.Ask(new InitializeEventStore
            {
                Settings = _settings,
                EventProcessors = _eventProcessors
            }) as EventStoreInitializationState;

            if (!state.Initialized)
                throw new Exception("Error initializing the event store.");

            return new EventStoreGateway
            {
                EventStore = esRef,
                CommandProcessors = state.CommandProcessors,
                EventProcessors = state.EventProcessors
            };
        }
    }

    public static class EventStoreSetupExtensions
    {
        public static EventStoreSetup SetupEventStore(this ActorSystem system)
        {
            return new EventStoreSetup(system);
        }
    }
}
