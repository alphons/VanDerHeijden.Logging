# VanDerHeijden.Logging

Core abstractions for high-performance batched logging in .NET 10.

## What's in this package

- `IBatchedLogWriter<T>` — implement this interface to create a custom log writer
- `BatchedLogger<T>` — background consumer that batches entries and calls your writer
- `BatchedLoggerProvider<T>` — `ILoggerProvider` adapter for use with `Microsoft.Extensions.Logging`

This package contains no writer implementation. Install one of the writer packages instead:

| Package | Target |
|---|---|
| `VanDerHeijden.Logging.File` | Daily rotating text files |
| `VanDerHeijden.Logging.MongoDb` | MongoDB collection |
| `VanDerHeijden.Logging.Sql` | SQL Server (SqlBulkCopy) |
| `VanDerHeijden.Logging.Redis` | Redis list (RPUSH) |

## Implementing a custom writer

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

Register it:

```csharp
builder.Logging.Services.AddSingleton<ILoggerProvider>(_ =>
{
    var writer = new MyWriter();
    var logger = new BatchedLogger<string>(writer, batchSize: 200, maxIdleMs: 4000);
    return new BatchedLoggerProvider<string>(logger, entryFactory: (msg, level) => msg);
});
```

## Repository

[https://github.com/alphons/VanDerHeijden.Logging](https://github.com/alphons/VanDerHeijden.Logging)
