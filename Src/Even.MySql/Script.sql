create table `Events` (
  GlobalSequence bigint not null primary key,
  EventID binary(16) not null,
  StreamID binary(20) not null,
  OriginalStreamID varchar(200) not null,
  EventType varchar(50) not null,
  UtcTimestamp datetime not null,
  Metadata blob not null,
  Payload mediumblob not null,
  PayloadFormat int not null
);

CREATE INDEX ix_Events_Streams ON `events` (StreamID);
CREATE UNIQUE INDEX uix_Events_Events ON `events` (EventID);

create table ProjectionIndex (
  ProjectionStreamID binary(20) not null,
  GlobalSequence bigint not null,
  primary key (ProjectionStreamID, GlobalSequence)
)
partition by key(StreamID)
partitions 5;

create table ProjectionCheckpoint (
  ProjectionStreamID binary(20) not null primary key,
  LastGlobalSequence bigint not null
);