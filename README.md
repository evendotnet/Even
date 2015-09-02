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
				Fail("User already exists");
			
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

```
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

Event processors are the core concept of Even. They listen to any events as they are persisted, but in order.
In fact, every time an event processor is created, a projection stream is automatically created for it based on the query it does
on the events. That stream is automatically sequenced and indexed, so any replays are fast and deterministic.

The query is defined by the events the processor decides to receive. For example:

```
public class ActiveUsers : EventProcessor
{
	public RegisteredUser()
	{
		OnEvent<UserRegistered>(e => ...);
		OnEvent<UserBlocked>(e => ...);
	}
}  
```

This processor creates a stream that contains only `UserRegistered` and `UserBlocked`, and emits a new predictable sequence number 
that starts with 1 and increments monotonically for that stream only. If two different event processors ask for the same events, event
in different order, they can share the same stream.

Because the stream is indexed in the database, replaying the events don't require replaying the entire event store.

You can use event processors to build projections in external stores. For example:

```
public class ActiveUsers : EventProcessor
{
	public RegisteredUser()
	{
		OnEvent<UserRegistered>(e => {
			// insert into sometable values (userid, streamsequence)
		});
	}
}  
```

Because the sequence is deterministic, you can restart the application, query the table to see the last sequence you stored
and resume the projection from that point automatically.

You can then project data to any external store, like Mongo, MySql, Azure, Amazon, etc and have zero startup time.

