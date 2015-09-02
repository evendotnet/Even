create table `events` (
  Checkpoint bigint not null auto_increment,
  EventID binary(16) not null,
  StreamID varchar(100) not null,
  StreamSequence int not null,
  EventName varchar(50) not null,
  UtcTimeStamp datetime not null,
  Headers blob not null,
  Payload mediumblob not null,
  primary key (Checkpoint)
);

CREATE UNIQUE INDEX uix_events_Events ON `events` (EventID);
CREATE UNIQUE INDEX uix_events_Streams ON `events` (StreamID, StreamSequence);

create table projectionstreams (
  ProjectionStreamID binary(20) not null,
  ProjectionStreamSequence int not null,
  Checkpoint bigint not null,
  primary key (ProjectionStreamID, ProjectionStreamSequence, Checkpoint)
)
partition by key(ProjectionStreamID)
partitions 5;

create table projectionstream_state (
  ProjectionStreamID binary(20) not null primary key,
  LastCheckpoint bigint not null
);