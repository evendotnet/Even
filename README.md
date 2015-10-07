# Even

Even is experimental event sourcing framework for .NET built on top of [Akka.NET](http://getakka.net). It aims to provide a solid foundation to build real world event sourcing applications on .NET.

> This project is currently an experiment. Feel free to open issues to discuss design, ask questions or contribute.

This implementation is based on ideas from [EventStore](https://geteventstore.com/), [NEventStore](https://github.com/NEventStore/NEventStore), [Eventuate](https://github.com/RBMHTechnology/eventuate) and some other sources.

## Goals

* Provide a simple way to write command and event processors
* Support indexed projections using any type of query
* Support externally stored projections using whatever you want (sql, nosql, akka persistence, etc)
* Supports distributed environments by ensuring event delivery
* Integrate well with Akka.NET
* Integrate well with regular .NET code
* Support any SQL database as backstore

## Even Architecture

Even works on top of Akka.NET, and leverages its power to distribute and queue messages, as well as clustering and remoting. There is no
need to use MSQM, NEventBus or any other library. Every component is an actor, and you can integrate easily with other services that
you build with Akka, but it is also easy to work without any knowledge of Akka.NET.

The architecture was heavily based on the need for projections, that is, the query side of event sourcing. Also, it does not use
Akka Persistence, as I found that while it works well for aggregates, it misses a lot of features required to do useful ES.

Even supports 3 basic constructs:

* Aggregates
* Command Processors
* Event Processors
* Projections

### Aggregates

Aggregates automatically start when required and are tied to a specific stream of events. They always replay those events
before processing commands, and ensure each command will be persisted in sequence before accepting new commands. 

```cs
// command
public class RegisterUser { public string Name { get; set; } }

// event
public class UserRegistered { public string Name { get; set; } }

public class User : Aggregate<UserState>
{
	public Product()
	{
		OnCommand<RegisterUser>(c => {
			
			if (State != null)
				Reject("User already exists");
			
			Persist(new UserRegistered { Name = c.Name });
		});
		
		OnEvent<UserRegistered>(e => {
			
			State = new State {
				Name = e.Name
			};
			
		});
	}
	
	public class UserState
	{
		public string Name { get; set; }
	}
}
```

### Command Processors

Command processors are similar to Aggregates in which they receive commands and persist them. The difference is that
command processors don't replay events neither require strict event ordering. You can use them to validate commands
and store events to any stream.

```cs
public class TrackDevice { public Guid DeviceID { get; set; } }

public class Tracker : CommandProcessor 
{
	public Tracker()
	{
		OnCommand<TrackDevice>(async c => {
		
			if (await IsTrackableDevice(c.DeviceID)) {
		
				var streamID = "devicetracking-" + c.DeviceID;
				Persist(streamID, new TrackingEnabled());
			
			}
		
		});
	}
}
``` 

### Event Processors

Event processors listen to events as they are persisted at runtime. They don't replay events or persist anything by
themselves. Their purpose is to create decoupled components that generate side effects, possibly sending new commands
to aggregates or communicate with other parts of the application.

```cs
// blocks users that had 3 failed login attempts in 15 minutes
public class UserBlocker : EventProcessor
{
	Cache _attempts = new Cache();

	public class UserBlocker()
	{
		OnEvent<UserLoginFailed>(e => {
		
			var cacheEntry = _attempts.GetOrAdd(e.UserID, TimeSpan.FromMinutes(15));
			
			if (cacheEntry.Counter > 3)
				Gateway.SendCommand<User>(e.UserID, new BlockUser("Too many failed login attempts."));
		});
	}
}

// notifies other parts of the system that an user came online
public class OnlineUserNotifier : EventProcessor
{
    public class OnlineUserNotifier()
	{
		// when the user logs in
		OnEvent<UserLoggedIn>(e => {
		
			// notify some actor to update the user stats if needed
			Context.ActorSelection("/user/statsmanager", new UpdateUserStats(e.UserID));

			// notify an "online tracker" that a new user is online
			Context.ActorSelection("/user/onlinetracker", new UserIsOnline(e.UserID));
		});
	}
}
```

### Projections

Projections are the core concept of Even. They listen to any events as they are persisted, but in order.
Every time a projection is created, a projection stream is automatically created for it based on the query it does
on the global event stream. That stream is automatically sequenced and indexed, so any replays are fast and deterministic.

The query is defined by the events the projection decides to receive. For example:

```cs
public class ActiveUsers : Projection
{
	public RegisteredUser()
	{
		OnEvent<UserRegistered>(e => ...);
		OnEvent<UserBlocked>(e => ...);
	}
}  
```

This projection creates a stream that contains only `UserRegistered` and `UserBlocked`, and emits a new predictable sequence number 
that starts with 1 and increments monotonically for that stream only. If two different projections ask for the same events, even
in different order, they can share the same stream.

Because the stream is indexed in the database, replaying the events don't require replaying the entire event store.
Projections can be in memory or stored in an external database.

```cs
public class ActiveUsers : Projection
{
	public RegisteredUser()
	{
		OnEvent<UserRegistered>(e => {
			// insert into sometable values (userid, streamId, streamSequence)
		});
	}

	protected override async Task<ProjectionState> GetLastKnownState()
	{
		// select StreamID, StreamSequence from sometable order by StreamSequence desc limit 1
		return new ProjectionState(streamId, streamSequence);
	}
	
	protected override async Task PrepareToRebuild()
	{
		// truncate table sometable
	}
}  
```

When the projection restarts, it can query the underlying store and ensure the stream is the same (the query didn't change).
If it changes, the projection is automatically rebuilt. if it didn't, the projection is replayed from the last
sequence it saw forward, making it really fast to restart.

You can then project data to any external store, like Mongo, MySql, Azure, Amazon, etc and have zero startup time.
