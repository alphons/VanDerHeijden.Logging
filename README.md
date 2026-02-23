# VanDerHeijden.Logging

High-performance, low-allocation batched logging for .NET 10, built on top of `Microsoft.Extensions.Logging`.

Log entries are written to an in-memory `Channel<T>` and flushed to the target in configurable batches, keeping the hot path (your application code) completely free of I/O.

## Packages

| Package | Description | NuGet |
|---|---|---|
| `VanDerHeijden.Logging` | Core abstractions | [![NuGet](https://img.shields.io/nuget/v/VanDerHeijden.Logging)](https://www.nuget.org/packages/VanDerHeijden.Logging) |
| `VanDerHeijden.Logging.File` | Daily rotating file writer | [![NuGet](https://img.shields.io/nuget/v/VanDerHeijden.Logging.File)](https://www.nuget.org/packages/VanDerHeijden.Logging.File) |
| `VanDerHeijden.Logging.MongoDb` | MongoDB collection writer | [![NuGet](https://img.shields.io/nuget/v/VanDerHeijden.Logging.MongoDb)](https://www.nuget.org/packages/VanDerHeijden.Logging.MongoDb) |
| `VanDerHeijden.Logging.Sql` | SQL Server writer (SqlBulkCopy) | [![NuGet](https://img.shields.io/nuget/v/VanDerHeijden.Logging.Sql)](https://www.nuget.org/packages/VanDerHeijden.Logging.Sql) |
| `VanDerHeijden.Logging.Redis` | Redis list writer (RPUSH) | [![NuGet](https://img.shields.io/nuget/v/VanDerHeijden.Logging.Redis)](https://www.nuget.org/packages/VanDerHeijden.Logging.Redis) |

## Architecture

```
Your application
      │
      ▼  logger.LogInformation(...)   [synchronous, no I/O]
 BatchedCategoryLogger<T>
      │
      ▼  channel.Writer.TryWrite(entry)
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
builder.Logging.AddFileLogger(logDirectory: "Logs");
```

Writes daily rotating files to the `Logs` directory as `log-yyyyMMdd.txt`.

### MongoDB

```bash
dotnet add package VanDerHeijden.Logging.MongoDb
```

```csharp
var mongoClient = new MongoClient("mongodb://localhost:27017");
var collection = mongoClient
    .GetDatabase("myapp")
    .GetCollection<LogEntry>("logs");

builder.Logging.AddMongoDbLogger(collection);
```

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
    Id        BIGINT IDENTITY PRIMARY KEY,
    Timestamp DATETIME2       NOT NULL,
    Level     NVARCHAR(20)    NOT NULL,
    Category  NVARCHAR(256)   NOT NULL,
    Message   NVARCHAR(MAX)   NOT NULL,
    Exception NVARCHAR(MAX)   NULL
);
```

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

## Configuration

`BatchedLogger<T>` accepts the following constructor parameters:

| Parameter | Default | Description |
|---|---|---|
| `batchSize` | 200 | Maximum entries per flush |
| `maxIdleMs` | 4000 | Maximum time (ms) between flushes when the batch is not full |
| `fullMode` | `Wait` | What to do when the channel is full (`Wait` or `DropOldest`) |

## Implementing a custom writer

Implement `IBatchedLogWriter<T>` and register it using `BatchedLoggerProvider<T>`:

```csharp
public sealed class MyWriter : IBatchedLogWriter<string>
{
    public async Task WriteBatchAsync(List<string> entries, CancellationToken ct)
    {
        // write entries to your target
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

```csharp
builder.Logging.Services.AddSingleton<ILoggerProvider>(_ =>
{
    var writer = new MyWriter();
    var logger = new BatchedLogger<string>(writer);
    return new BatchedLoggerProvider<string>(logger, (msg, level) => msg);
});
```

## Performance

Benchmarked with [BenchmarkDotNet](https://benchmarkdotnet.org/) on .NET 10.0.3 (X64 RyuJIT AVX-512), Windows 11.
Each figure is the mean time per `WriteBatchAsync` call, averaged over 2 000 consecutive calls.

| BatchSize | MessageLength | Mean/flush | Allocated |
|----------:|:-------------:|-----------:|----------:|
| 1         | 80 B          |    27.9 µs |     477 B |
| 10        | 256 B         |    26.6 µs |     477 B |
| 100       | 1 024 B       |   144.0 µs |     756 B |
| 500       | 1 024 B       |   704.5 µs |   2 436 B |

Allocation is flat (~477 B) for all batches up to 100 messages regardless of message length — zero GC pressure in typical use. The `Write()` call itself is non-blocking and allocates nothing beyond the log entry.

> Hardware: Intel Core i5-1035G1 1.00 GHz · Full results in [`VanDerHeijden.Logging.File`](src/VanDerHeijden.Logging.File/README.md#performance).

## License

MIT

## Repository

[https://github.com/alphons/VanDerHeijden.Logging](https://github.com/alphons/VanDerHeijden.Logging)
