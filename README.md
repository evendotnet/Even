# Even

Even is experimental [event sourcing](http://docs.geteventstore.com/introduction/event-sourcing-basics/) framework for .NET built on top of [Akka.NET](http://getakka.net). It aims to provide a solid foundation to build real world event sourcing applications on .NET.

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
