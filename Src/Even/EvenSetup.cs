using Akka.Actor;
using Even.Messages;
using Even.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class EvenSetup
    {
        ActorSystem _system;
        IEventStore _store;
        ISerializer _serializer;
        GlobalOptions _options;
        List<StartEventProcessor> _eventProcessors = new List<StartEventProcessor>();
        List<StartProjection> _projections = new List<StartProjection>();

        public EvenSetup(ActorSystem system)
        {
            Argument.RequiresNotNull(system, nameof(system));

            this._system = system;
        }

        public EvenSetup UseStore(IEventStore store)
        {
            Argument.RequiresNotNull(store, nameof(store));

            _store = store;

            return this;
        }

        public EvenSetup UseSerializer(ISerializer serializer)
        {
            Argument.RequiresNotNull(serializer, nameof(serializer));

            _serializer = serializer;

            return this;
        }
        public EvenSetup UseOptions(GlobalOptions options)
        {
            Argument.RequiresNotNull(options, nameof(options));

            _options = options;

            return this;
        }

        public EvenSetup AddProjections(Func<IEnumerable<Type>> func)
        {
            Argument.RequiresNotNull(func, nameof(func));

            foreach (var t in func())
                _projections.Add(new StartProjection(t));

            return this;
        }

        public EvenSetup AddProjection<T>()
            where T : Projection
        {
            _projections.Add(new StartProjection(typeof(T)));

            return this;
        }

        public EvenSetup AddEventProcessors(Func<IEnumerable<Type>> func)
        {
            Argument.RequiresNotNull(func, nameof(func));

            foreach(var t in func())
                _eventProcessors.Add(new StartEventProcessor(t));

            return this;
        }

        public EvenSetup AddEventProcessor<T>()
            where T : EventProcessor
        {
            _eventProcessors.Add(new StartEventProcessor(typeof(T)));

            return this;
        }

        public async Task<EvenGateway> Start(string name = null)
        {
            var options = _options ?? new GlobalOptions();
            var store = _store ?? new InMemoryStore();
            var serializer = _serializer ?? new DefaultSerializer();

            var startInfo = new EvenStartInfo(store, serializer, options);
            startInfo.Projections.AddRange(_projections);
            startInfo.EventProcessors.AddRange(_eventProcessors);

            var props = EvenMaster.CreateProps(startInfo);
            var master = _system.ActorOf(props, name);
            var timeout = TimeSpan.FromSeconds(5);

            var services = (EvenServices) await master.Ask(new GetEvenServices(), timeout);

            return new EvenGateway(services, _system, options);
        }
    }

    public static class EvenSetupExtensions
    {
        public static EvenSetup SetupEven(this ActorSystem system)
        {
            return new EvenSetup(system);
        }
    }
}
