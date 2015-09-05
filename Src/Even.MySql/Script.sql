create table `events` (
  GlobalSequence bigint not null,
  EventID binary(16) not null,
  StreamID binary(20) not null,
  OriginalStreamID varchar(200) not null,
  StreamSequence int not null,
  EventType varchar(50) not null,
  UtcTimestamp datetime not null,
  Metadata blob not null,
  Payload mediumblob not null,
  PayloadFormat int not null,
  primary key (GlobalSequence)
);

CREATE UNIQUE INDEX uix_events_Events ON `events` (EventID);
CREATE UNIQUE INDEX uix_events_Streams ON `events` (StreamID, StreamSequence);

create table projectionstreams (
  ProjectionStreamID binary(20) not null,
  ProjectionStreamSequence int not null,
  GlobalSequence bigint not null,
  primary key (ProjectionStreamID, ProjectionStreamSequence)
)
partition by key(ProjectionStreamID)
partitions 5;

create table checkpoints (
  ProjectionStreamID binary(20) not null primary key,
  LastGlobalSequence bigint not null
);