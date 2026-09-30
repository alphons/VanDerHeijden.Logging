# VanDerHeijden.Logging

High-performance, low-allocation batched logging for .NET 10, built on top of `Microsoft.Extensions.Logging`.

Log entries are written to an in-memory `Channel<T>` and flushed to the target in configurable batches, keeping the hot path (your application code) completely free of I/O.

## Packages

| Package | Version | Description | NuGet |
|---|---|---|---|
| `VanDerHeijden.Logging` | 10.1.0 | Core abstractions | [![NuGet](https://img.shields.io/nuget/v/VanDerHeijden.Logging)](https://www.nuget.org/packages/VanDerHeijden.Logging) |
| `VanDerHeijden.Logging.File` | 10.1.0 | Daily rotating file writer | [![NuGet](https://img.shields.io/nuget/v/VanDerHeijden.Logging.File)](https://www.nuget.org/packages/VanDerHeijden.Logging.File) |
| `VanDerHeijden.Logging.MongoDb` | 10.1.0 | MongoDB collection writer | [![NuGet](https://img.shields.io/nuget/v/VanDerHeijden.Logging.MongoDb)](https://www.nuget.org/packages/VanDerHeijden.Logging.MongoDb) |
| `VanDerHeijden.Logging.Sql` | 10.1.0 | SQL Server writer (SqlBulkCopy) | [![NuGet](https://img.shields.io/nuget/v/VanDerHeijden.Logging.Sql)](https://www.nuget.org/packages/VanDerHeijden.Logging.Sql) |
| `VanDerHeijden.Logging.Redis` | 10.1.0 | Redis list writer (RPUSH) | [![NuGet](https://img.shields.io/nuget/v/VanDerHeijden.Logging.Redis)](https://www.nuget.org/packages/VanDerHeijden.Logging.Redis) |

## Upgrading from 10.0.x

10.1.0 adds structured logging (message template, properties, event id) and contains breaking changes:

| Area | What changed | What to do |
|---|---|---|
| Entry classes | `RedisLogEntry`, `SqlLogEntry`, the MongoDB `LogEntry` and `HttpLogContext` are removed | Use the shared `VanDerHeijden.Logging.LogEntry` |
| Custom writers | `BatchedLoggerProvider<T>` takes `Func<LogEntry, T>` instead of the five-argument factory | Rewrite the factory, e.g. `entry => entry` |
| MongoDB | `AddMongoDbLogger` takes `IMongoCollection<BsonDocument>` instead of `IMongoCollection<LogEntry>`; indexes are created at startup | Pass `GetCollection<BsonDocument>(...)`; set `CreateIndexes = false` to opt out |
| SQL Server | The writer maps four new columns: `EventId`, `EventName`, `MessageTemplate`, `Properties` | Run the [migration script](src/VanDerHeijden.Logging.Sql/README.md#migrating-an-existing-table) **before** upgrading, otherwise inserts fail and entries are lost |
| Redis | JSON is written by the shared `LogEntryJsonWriter`: new fields, and `null` fields are omitted instead of written as `null` | Make consumers tolerate missing fields |
| File | Text lines now contain the log level and a UTC timestamp with a `Z` suffix; files rotate on the UTC date | Adjust parsers of the text format |
| Backpressure | `BatchedLogger.Write` now really blocks in `Wait` mode (File, SQL) when the channel is full; before, entries were silently dropped | Nothing, but a stalled target can now slow the application down |

## Architecture

```
Your application
      │
      ▼  logger.LogInformation(...)   [synchronous, no I/O]
 BatchedCategoryLogger<T>          builds a LogEntry (UTC timestamp, level, event id,
      │                            message, template, properties, HTTP fields)
      ▼  batchedLogger.Write(entryFactory(logEntry))
 Channel<T>  (bounded, in-memory)
      │
      ▼  background consumer task
 BatchedLogger<T>
      │  accumulates up to batchSize entries or maxIdleMs timeout
      ▼
 IBatchedLogWriter<T>.WriteBatchAsync(...)
      │
      ▼
 FileLogWriter / MongoDbLogWriter / SqlLogWriter / RedisLogWriter
```

## Quick start

Install only the writer you need and register it in `Program.cs`. Each writer is independent — you can combine multiple writers simultaneously.

### File

```bash
dotnet add package VanDerHeijden.Logging.File
```

```csharp
builder.Logging.AddFileLogger(logDirectory: "Logs");                          // text
builder.Logging.AddFileLogger(logDirectory: "Logs", format: LogFormat.Json);  // JSON Lines
```

Writes daily rotating files (UTC date) to the `Logs` directory as `log-yyyyMMdd.txt`, or as
`log-yyyyMMdd.jsonl` with one JSON object per line when `LogFormat.Json` is selected.

### MongoDB

```bash
dotnet add package VanDerHeijden.Logging.MongoDb
```

```csharp
var mongoClient = new MongoClient("mongodb://localhost:27017");
var collection = mongoClient
    .GetDatabase("myapp")
    .GetCollection<BsonDocument>("logs");

builder.Logging.AddMongoDbLogger(collection, options => options.RetentionDays = 30); // TTL is optional
```

Structured properties are stored as a typed BSON subdocument, and the indexes (`Timestamp`, `Level + Timestamp`,
`Category + Timestamp`, wildcard on `Properties`) are created at startup. See the
[MongoDb README](src/VanDerHeijden.Logging.MongoDb/README.md) for the type mapping and options.

### SQL Server

```bash
dotnet add package VanDerHeijden.Logging.Sql
```

```csharp
builder.Logging.AddSqlLogger(
    connectionString: "Server=.;Database=MyApp;Integrated Security=true;",
    tableName: "Logs");
```

Required table schema:

```sql
CREATE TABLE Logs (
    Id              BIGINT IDENTITY PRIMARY KEY,
    Timestamp       DATETIME2       NOT NULL,  -- UTC
    Level           NVARCHAR(20)    NOT NULL,
    EventId         INT             NOT NULL,
    EventName       NVARCHAR(256)   NULL,
    Category        NVARCHAR(256)   NOT NULL,
    Message         NVARCHAR(MAX)   NOT NULL,
    MessageTemplate NVARCHAR(MAX)   NULL,
    Properties      NVARCHAR(MAX)   NULL,      -- JSON object
    Exception       NVARCHAR(MAX)   NULL,
    Path            NVARCHAR(1024)  NULL,
    Method          NVARCHAR(10)    NULL,
    ClientIp        NVARCHAR(45)    NULL,
    Referer         NVARCHAR(2048)  NULL,
    UserAgent       NVARCHAR(512)   NULL,
    SessionId       NVARCHAR(256)   NULL,
    SessionGuid     NVARCHAR(36)    NULL
);
```

Existing tables need the four new columns (`EventId`, `EventName`, `MessageTemplate`, `Properties`) — see the
[migration script](src/VanDerHeijden.Logging.Sql/README.md#migrating-an-existing-table).

### Redis

```bash
dotnet add package VanDerHeijden.Logging.Redis
```

```csharp
var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");

builder.Logging.AddRedisLogger(
    database: redis.GetDatabase(),
    listKey: "logs",
    ttl: TimeSpan.FromDays(7));   // optional: auto-expire the key
```

Entries are pushed to a Redis list as JSON via `RPUSH` and can be consumed by any Redis-compatible consumer (Logstash, a worker service, etc.) via `BLPOP`.

## The shared `LogEntry`

Every log call is captured as one `VanDerHeijden.Logging.LogEntry`, the source for the MongoDB, Redis, SQL and file writers
(the former `RedisLogEntry`, `SqlLogEntry` and MongoDB `LogEntry` classes are gone):

| Property | Description |
|---|---|
| `Timestamp` | `DateTime`, always UTC |
| `Level` | `LogLevel` (stored as its name, e.g. `Information`) |
| `EventId` / `EventName` | From the `EventId` passed to the log call (`0` / `null` when absent) |
| `Category` | Logger category name |
| `Message` | Formatted message |
| `MessageTemplate` | The original template (`{OriginalFormat}`), e.g. `User {UserId} logged in` |
| `Properties` | `Dictionary<string, object?>` with the named template arguments, e.g. `UserId = 42`; `null` when there are none |
| `Exception` | `exception.ToString()`, or `null` |
| `Path`, `Method`, `ClientIp`, `Referer`, `UserAgent`, `SessionId`, `SessionGuid` | HTTP fields, see below |

Property values are snapshotted on the logging thread, because batches are written later and objects may have changed
by then: strings, booleans, `int`, `long`, `double`, `decimal`, `Guid` and `DateTime` (converted to UTC) keep their
type, smaller numeric types are widened, enums are stored by name, and `TimeSpan` and other formattable values are stored as strings.

Objects, records and collections are stored as nested JSON, so `logger.LogInformation("Order for {Customer}", customer)`
stores `"Customer": { "Name": "Alice", "Number": 7, "Tags": ["vip"] }` (a subdocument in MongoDB) instead of the
`ToString()` text. To keep the logging call cheap, only a **shallow clone** of the object is taken at log time; the
JSON is produced later, on the background writer thread:

- Top-level members are frozen at log time: changing `customer.Name` afterwards does not affect the log.
- Nested objects and collections are shared with the original: changing `customer.Address.City` or adding to
  `customer.Tags` before the batch is written can show up in the log. Log immutable data (records) or the specific
  values you need when that matters.
- Objects that must not be cloned — anything `IDisposable` or with a finalizer, types, delegates, exceptions, tasks —
  are stored as their `ToString()` text, as are values that cannot be serialized, have no public properties or
  exceed 32 KB of JSON.

Enums inside objects are written by name. The formatted `Message` is unaffected: it still contains the `ToString()`
text, as produced by `Microsoft.Extensions.Logging`.

JSON output (file, Redis, the SQL `Properties` column) is produced by the shared `LogEntryJsonWriter`, so the field
names are the same everywhere: `timestamp`, `level`, `eventId`, `eventName`, `category`, `message`, `messageTemplate`,
`properties`, `exception`, `path`, `method`, `clientIp`, `referer`, `userAgent`, `sessionId`, `sessionGuid`.
Fields that are `null` are omitted.

## HTTP context enrichment

All writers automatically capture request metadata when `IHttpContextAccessor` is available:

```csharp
builder.Services.AddHttpContextAccessor(); // enable once in Program.cs
```

`SessionId` requires session middleware to be configured (`builder.Services.AddSession()` and `app.UseSession()`); without an active session it is `null`.

The following fields are added to each log entry when an HTTP request is active:

| Field | Example |
|---|---|
| `Path` | `/api/users/login` |
| `Method` | `POST` |
| `ClientIp` | `203.0.113.42` (respects `X-Forwarded-For`) |
| `Referer` | `https://example.com` |
| `UserAgent` | `Mozilla/5.0 ...` |
| `SessionId` | `af3d9e...` (from `HttpContext.Session`) |
| `SessionGuid` | `3fa85f64-5717-4562-b3fc-2c963f66afa6` (from `HttpContext.Session["SessionGuid"]`, when set by the application) |

Outside an HTTP context (background services, hosted workers) all HTTP fields are `null` / omitted.

## Configuration

`BatchedLogger<T>` accepts the following constructor parameters:

| Parameter | Default | Description |
|---|---|---|
| `batchSize` | 200 | Maximum entries per flush |
| `maxIdleMs` | 4000 | Maximum time (ms) between flushes when the batch is not full |
| `fullMode` | `Wait` | What to do when the channel is full (`Wait` or `DropOldest`) |

The channel holds 10 000 entries. With `Wait` (File, SQL) a logging call **blocks** while the channel is full, so no
entry is lost but a stalled target slows the application down. With `DropOldest` (MongoDB, Redis) logging never blocks
and the oldest entries are discarded instead.

## Implementing a custom writer

Implement `IBatchedLogWriter<T>` and register it using `BatchedLoggerProvider<T>`:

```csharp
public sealed class MyWriter : IBatchedLogWriter<LogEntry>
{
    public async Task WriteBatchAsync(List<LogEntry> entries, CancellationToken ct)
    {
        // write entries to your target
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

```csharp
builder.Logging.Services.AddSingleton<ILoggerProvider>(sp =>
{
    var httpContextAccessor = sp.GetService<IHttpContextAccessor>(); // optional
    var writer = new MyWriter();
    var logger = new BatchedLogger<LogEntry>(writer);
    return new BatchedLoggerProvider<LogEntry>(
        logger,
        entryFactory: entry => entry,
        httpContextAccessor);
});
```

`entryFactory` is a `Func<LogEntry, T>` that runs on the logging thread. Return the entry itself, or project it to
your own type (e.g. a pre-formatted `string`, as the text file logger does) and implement `IBatchedLogWriter<T>` for that type.

## Performance

Benchmarked with [BenchmarkDotNet](https://benchmarkdotnet.org/) 0.15.8 on .NET 10.0.12 (X64 RyuJIT), Windows 10.
All times are in milliseconds per 1 000 operations.

### Cost of logging, on the calling thread

What 1 000 `logger.LogInformation(...)` calls cost the thread that logs. Writing happens later, in the background.

| 1 000 log calls with          | Time    | Allocated per call |
|-------------------------------|--------:|-------------------:|
| a plain message               | 0.45 ms |              257 B |
| 3 primitive arguments         | 1.9 ms  |            1 117 B |
| a Guid and an object (record) | 3.9 ms  |            3 314 B |

An object argument is only shallow-cloned by the caller; serializing it to JSON or BSON is done by the writer thread.
When the channel is full the calls get slower (garbage collection of the queued entries): about 16 ms per 1 000
calls was measured with a deliberately slow writer.

### Cost of writing, on the background thread

| 1 000 entries with an object                     | Time    |
|--------------------------------------------------|--------:|
| written as JSON lines to the buffer              | 1.0 ms  |
| converted to MongoDB documents                   | 6.8 ms  |
| 100 000 entries logged and written to `.jsonl`   | 13.7 ms |
| inserted into MongoDB (batches of 100, 4 indexes)| 267 ms  |

The first two rows exclude the one-time JSON serialization of the object itself. The MongoDB insert rate (about
3 700 documents per second on this machine, local server) is what limits sustained logging: the MongoDB logger uses
`DropOldest`, so a burst that outruns the writer by more than the 10 000-entry channel loses its oldest entries.

### Text file writer

Time for 1 000 `WriteBatchAsync` calls (one flush to disk each).

| BatchSize | MessageLength | 1 000 flushes | Allocated per flush |
|----------:|:-------------:|--------------:|--------------------:|
| 1         | 80 B          |       47.9 ms |               476 B |
| 10        | 256 B         |       60.1 ms |               476 B |
| 100       | 1 024 B       |      212.9 ms |               757 B |
| 500       | 1 024 B       |    1 175.6 ms |             2 437 B |

Enqueueing 10 000 pre-formatted messages takes about 0.55 ms. Past the channel capacity of 10 000 entries `Wait`
mode applies backpressure instead of dropping: 100 000 messages take about 136 ms and allocate 590 KB in total.

> Hardware: Intel Core i7-3520M 2.90 GHz (2 cores / 4 threads) · Full file writer results in [`VanDerHeijden.Logging.File`](src/VanDerHeijden.Logging.File/README.md#performance).

## License

MIT

## Repository

[https://github.com/alphons/VanDerHeijden.Logging](https://github.com/alphons/VanDerHeijden.Logging)
