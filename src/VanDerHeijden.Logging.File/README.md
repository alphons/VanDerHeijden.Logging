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
fields are omitted. Property values are written as JSON strings, numbers or booleans; other types are written
as their string representation. The JSON is produced by the shared `LogEntryJsonWriter` from the core package.

Register `IHttpContextAccessor` in `Program.cs` to enable HTTP enrichment:

```csharp
builder.Services.AddHttpContextAccessor();
```

## Performance

Benchmarked with [BenchmarkDotNet](https://benchmarkdotnet.org/) on .NET 10 (X64 RyuJIT AVX-512).
Each measurement is the mean time per `WriteBatchAsync` call, averaged over 2 000 consecutive calls.

| BatchSize | MessageLength | Mean     | Allocated |
|----------:|:-------------:|---------:|----------:|
| 1         | 80 B          |  27.9 µs |     477 B |
| 1         | 256 B         |  25.2 µs |     477 B |
| 1         | 1 024 B       |  22.1 µs |     476 B |
| 10        | 80 B          |  24.2 µs |     476 B |
| 10        | 256 B         |  26.6 µs |     477 B |
| 10        | 1 024 B       |  36.8 µs |     477 B |
| 100       | 80 B          |  39.1 µs |     477 B |
| 100       | 256 B         |  53.9 µs |     476 B |
| 100       | 1 024 B       | 144.0 µs |     756 B |
| 500       | 80 B          | 105.5 µs |     477 B |
| 500       | 256 B         | 160.5 µs |     757 B |
| 500       | 1 024 B       | 704.5 µs |   2 436 B |

**Key characteristics:**
- Allocation is flat (~477 B) for batches up to 100 messages of any length — zero GC pressure in typical use.
- Per-call time is dominated by `FlushAsync` (~22 µs) at small batch sizes; write volume becomes the cost at larger batches.

> Hardware: Intel Core i5-1035G1 1.00 GHz, Windows 11, .NET 10.0.3

## Repository

[https://github.com/alphons/VanDerHeijden.Logging](https://github.com/alphons/VanDerHeijden.Logging)
