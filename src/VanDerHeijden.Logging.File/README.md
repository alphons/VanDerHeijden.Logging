# VanDerHeijden.Logging.File

File log writer for [VanDerHeijden.Logging](https://www.nuget.org/packages/VanDerHeijden.Logging).

Writes batched log entries to **daily rotating files** — plain text or JSON Lines — using async file I/O with a 64 KB write buffer. A new file is opened automatically at midnight (UTC) without restarting the application.

## Installation

```bash
dotnet add package VanDerHeijden.Logging.File
```

## Usage

```csharp
builder.Logging.AddFileLogger(logDirectory: "Logs");                          // text
builder.Logging.AddFileLogger(logDirectory: "Logs", format: LogFormat.Json);  // JSON Lines
```

Log files are written to the `Logs` directory (relative to the working directory) as
`log-yyyyMMdd.txt` (`LogFormat.Text`) or `log-yyyyMMdd.jsonl` (`LogFormat.Json`).
Timestamps and the date in the file name are UTC.

## Options

| Parameter | Default | Description |
|---|---|---|
| `logDirectory` | `"Logs"` | Directory where log files are created |
| `format` | `LogFormat.Text` | `Text` or `Json` (JSON Lines) |

The file logger uses `batchSize` 200, `maxIdleMs` 4000 and `fullMode` `Wait`: when the internal
channel (10 000 entries) is full, logging calls block until the writer catches up — nothing is dropped.

## Text format

Without HTTP context:
```
2026-02-22 14:03:12.456Z [Information] [MyApp.Service] User 42 logged in
```

With HTTP context (when `IHttpContextAccessor` is registered) — method, path, client IP, session id, session GUID:
```
2026-02-22 14:03:12.456Z [Information] [POST /api/users/login 203.0.113.42 af3d9e 3fa85f64-5717-4562-b3fc-2c963f66afa6] [MyApp.Service] User 42 logged in
```

An exception, if any, follows on the next lines.

## JSON format

One JSON object per line, written with `Utf8JsonWriter`. For `logger.LogInformation("User {UserId} logged in", 42)`:

```json
{"timestamp":"2026-02-22T14:03:12.4561234Z","level":"Information","eventId":0,"category":"MyApp.Service","message":"User 42 logged in","messageTemplate":"User {UserId} logged in","properties":{"UserId":42},"path":"/api/users/login","method":"POST","clientIp":"203.0.113.42"}
```

`eventName`, `exception`, `referer`, `userAgent`, `sessionId` and `sessionGuid` are included when set; `null`
fields are omitted. Property values are written as JSON strings, numbers or booleans; objects and collections are written as
nested JSON objects and arrays (a shallow clone taken at log time, serialized by the writer; `ToString()` when that is not possible). The JSON is produced by the shared `LogEntryJsonWriter` from the core package.

Register `IHttpContextAccessor` in `Program.cs` to enable HTTP enrichment:

```csharp
builder.Services.AddHttpContextAccessor();
```

## Performance

Benchmarked with [BenchmarkDotNet](https://benchmarkdotnet.org/) 0.15.8 on .NET 10.0.12 (X64 RyuJIT).

### FileLogWriter

Time for 1 000 `WriteBatchAsync` calls (text format, one flush to disk each), in milliseconds.

| BatchSize | MessageLength | 1 000 flushes | Allocated per flush |
|----------:|:-------------:|--------------:|--------------------:|
| 1         | 80 B          |    47.9 ms |     476 B |
| 1         | 256 B         |    46.6 ms |     476 B |
| 1         | 1 024 B       |    51.1 ms |     476 B |
| 10        | 80 B          |    53.5 ms |     476 B |
| 10        | 256 B         |    60.1 ms |     476 B |
| 10        | 1 024 B       |    67.2 ms |     476 B |
| 100       | 80 B          |    88.6 ms |     476 B |
| 100       | 256 B         |   105.4 ms |     476 B |
| 100       | 1 024 B       |   212.9 ms |     757 B |
| 500       | 80 B          |   139.3 ms |     476 B |
| 500       | 256 B         |   262.2 ms |     757 B |
| 500       | 1 024 B       | 1 175.6 ms |   2 437 B |

**Key characteristics:**
- Allocation is flat (about 476 B) for batches up to 100 messages of up to 256 B — zero GC pressure in typical use.
- Time is dominated by `FlushAsync` (about 47 ms per 1 000 flushes) at small batch sizes; write volume becomes the cost at larger batches.

### End-to-end (BatchedLogger + FileLogWriter)

Time to enqueue N pre-formatted messages with `Write()` (`fullMode: Wait`, batch size 200).

| Messages | Time      | Allocated |
|---------:|----------:|----------:|
| 1 000    |   0.23 ms |     16 KB |
| 10 000   |   0.55 ms |    257 KB |
| 100 000  |    136 ms |    590 KB |

Up to the channel capacity (10 000 entries) enqueueing costs well under 1 ms per 1 000 messages. Beyond that, `Wait`
mode applies backpressure: callers block until the writer catches up, so the 100 000 case measures the file writer
(about 1.4 ms per 1 000 messages, 500 flushes to disk) rather than the enqueue. Nothing is dropped, and the consumer
itself allocates nothing per entry.

### JSON Lines with objects

100 000 log calls that each carry a Guid, an int and an object (record with a nested object and an array), written
to `.jsonl` in `Wait` mode: 1.37 s in total (13.7 ms per 1 000 entries), nothing dropped. The logging call itself
costs about 3.9 ms per 1 000 calls; the rest is serialization and disk I/O on the writer thread.

> Hardware: Intel Core i7-3520M 2.90 GHz (Ivy Bridge, 2 cores / 4 threads), Windows 10, .NET 10.0.12

## Repository

[https://github.com/alphons/VanDerHeijden.Logging](https://github.com/alphons/VanDerHeijden.Logging)
