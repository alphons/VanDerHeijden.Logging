# VanDerHeijden.Logging

Core abstractions for high-performance batched logging in .NET 10.

## What's in this package

- `LogEntry` — the structured log entry shared by all writers
- `LogEntryJsonWriter` — shared `Utf8JsonWriter`-based JSON serializer for `LogEntry`
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
public sealed class MyWriter : IBatchedLogWriter<LogEntry>
{
    public async Task WriteBatchAsync(List<LogEntry> entries, CancellationToken ct)
    {
        // write entries to your target
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

Register it:

```csharp
builder.Logging.Services.AddSingleton<ILoggerProvider>(sp =>
{
    var httpContextAccessor = sp.GetService<IHttpContextAccessor>(); // optional
    var writer = new MyWriter();
    var logger = new BatchedLogger<LogEntry>(writer, batchSize: 200, maxIdleMs: 4000);
    return new BatchedLoggerProvider<LogEntry>(
        logger,
        entryFactory: entry => entry,
        httpContextAccessor);
});
```

The `entryFactory` is a `Func<LogEntry, T>` that runs on the logging thread; return the entry itself or project it
to your own type (for example a pre-formatted `string`).

## LogEntry

| Property | Description |
|---|---|
| `Timestamp` | `DateTime`, always UTC |
| `Level` | `LogLevel` |
| `EventId` / `EventName` | From the `EventId` passed to the log call |
| `Category` | Logger category name |
| `Message` | Formatted message |
| `MessageTemplate` | The original template (`{OriginalFormat}`), e.g. `User {UserId} logged in` |
| `Properties` | `Dictionary<string, object?>` with the named template arguments; `null` when there are none |
| `Exception` | `exception.ToString()`, or `null` |
| `Path`, `Method`, `ClientIp`, `Referer`, `UserAgent`, `SessionId`, `SessionGuid` | Request metadata; `null` when no HTTP context is active or `IHttpContextAccessor` is not registered |

Property values are snapshotted at log time. They keep their type when they are a `string`, `bool`, `int`, `long`,
`double`, `decimal`, `Guid` or `DateTime` (converted to UTC); smaller numeric types are widened, enums are stored by
name, and `TimeSpan` and other formattable values are stored as strings.

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

## LogEntryJsonWriter

`LogEntryJsonWriter` serializes entries with `Utf8JsonWriter` into a reusable buffer and is the single place where the
JSON field names are defined. It is not thread-safe; use one instance per log writer.

```csharp
var json = new LogEntryJsonWriter();
json.Clear();
json.WriteLine(entry);                  // one JSON object + '\n' (JSON Lines)
await stream.WriteAsync(json.WrittenMemory, ct);

json.Clear();
json.WriteProperties(entry.Properties); // only the properties object
string propertiesJson = json.ToString();
```

## Backpressure

`BatchedLogger<T>` is configured with `BatchedLoggerOptions`: `BatchSize` (default 200), `MaxIdleMs` (4000),
`QueueCapacity` (10 000) and `FullMode` (`Wait`). All writer packages expose the same options.

`BatchedLogger<T>.Write` honours `FullMode`. With `BoundedChannelFullMode.Wait`
(the default) the call blocks while the queue is full, so nothing is dropped; with the drop modes
it never blocks.

## Repository

[https://github.com/alphons/VanDerHeijden.Logging](https://github.com/alphons/VanDerHeijden.Logging)
