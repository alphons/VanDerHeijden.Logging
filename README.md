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

Install only the writer you need:

```bash
dotnet add package VanDerHeijden.Logging.File
```

Register in `Program.cs`:

```csharp
builder.Logging.AddFileLogger(logDirectory: "Logs");
```

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

Measured on an Intel Core i7-3520M (Ivy Bridge), .NET 10.0.3, Windows 10:

| BatchSize | MessageLength | Mean/flush | Allocated |
|----------:|-------------:|----------:|----------:|
| 1 | 80 B | 48 µs | 475 B |
| 10 | 256 B | 66 µs | 476 B |
| 100 | 1024 B | 310 µs | 761 B |
| 500 | 1024 B | 1,335 µs | 2,441 B |

The `Write()` call itself is non-blocking and allocates nothing beyond the log entry.

## License

MIT

## Repository

[https://github.com/alphons/VanDerHeijden.Logging](https://github.com/alphons/VanDerHeijden.Logging)
