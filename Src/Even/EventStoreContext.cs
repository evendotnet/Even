using Akka.Actor;

namespace Even
{
    public class EventStoreContext
    {
        public EventStoreContext(string storeID, IActorRef root)
        {
            Root = root;
            StoreID = storeID;
        }

        public string StoreID { get; }
        public IActorRef Root { get; }
        public IActorRef Writer { get; }
        public IActorRef Reader { get; }

        public EventStoreSettings Settings { get; }
    }
}