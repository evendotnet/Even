using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Even
{
    public class WhoIs<T>
    {
        public WhoIs(string storeId, IActorRef replyTo)
        {
            Contract.Requires(storeId != null);
            Contract.Requires(replyTo != null);

            this.StoreID = storeId;
            this.ReplyTo = replyTo;
        }

        public string StoreID { get; }
        public IActorRef ReplyTo { get; }
    }

    public class ServiceOf<T>
    {
        public ServiceOf(IActorRef actorRef)
        {
            Contract.Requires(actorRef != null);
            this.ActorRef = actorRef;
        }

        IActorRef ActorRef { get; }
    }

    /// <summary>
    /// Requests information about active event stores.
    /// </summary>
    public class DiscoverEventStore
    {
        public string StoreID { get; set; }
    }

    /// <summary>
    /// Response from a DiscoverEventStore request.
    /// </summary>
    public class EventStoreIdentity
    {
        public EventStoreIdentity(EventStoreContext context)
        {
            Context = context;
        }

        public EventStoreContext Context { get; }
    }
}
