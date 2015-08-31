using Akka.Actor;
using Even.Messages;
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
        private Dictionary<Type, Type> _aggregates = new Dictionary<Type, Type>();
        private List<Type> _projections = new List<Type>();
        private EventStoreSettings _settings = new EventStoreSettings();

        public EventStoreSetup(ActorSystem system)
        {
            Contract.Requires(system != null);
            this._system = system;
        }

        public EventStoreSetup UseStorage(IStorageDriver driver)
        {
            Contract.Requires(driver != null);
            _settings.StorageDriver = driver;
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

        public EventStoreSetup AddProjection<T>()
            where T : Projection
        {
            _projections.Add(typeof(T));

            return this;
        }

        public EventStoreSetup RegisterAggregate<T, TAggregate>()
            where TAggregate : Aggregate<T>
            where T : new()
        {
            return RegisterAggregate(typeof(T), typeof(TAggregate));
        }

        public EventStoreGateway Start()
        {
            var props = CreateProps();

            var esRef = _system.ActorOf(props, "EventStore");

            esRef.Tell(new InitializeEventStore
            {
                Projections = _projections
            });

            var aggregatePath = esRef.Path.ToString() + "/" + EventStore.AggregateSupervisorPath;

            var gateway = new EventStoreGateway
            {
                EventStore = esRef,
                Aggregates = _system.ActorSelection(aggregatePath)
            };

            return gateway;
        }

        private Props CreateProps()
        {
            return Props.Create<EventStore>(_settings);
        }

        public EventStoreSetup RegisterProjection(Type projectionType)
        {
            _projections.Add(projectionType);
            return this;
        }

        public EventStoreSetup RegisterAggregate(Type stateType, Type aggregateType)
        {
            _aggregates.Add(stateType, aggregateType);
            return this;
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
