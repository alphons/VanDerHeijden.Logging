# VanDerHeijden.Logging.File

File log writer for [VanDerHeijden.Logging](https://www.nuget.org/packages/VanDerHeijden.Logging).

Writes batched log entries to **daily rotating text files** using async file I/O with a 64 KB write buffer. A new file is opened automatically at midnight without restarting the application.

## Installation

```bash
dotnet add package VanDerHeijden.Logging.File
```

## Usage

```csharp
builder.Logging.AddFileLogger(logDirectory: "Logs");
```

Log files are written to the `Logs` directory (relative to the working directory) as `log-yyyyMMdd.txt`.

## Options

| Parameter | Default | Description |
|---|---|---|
| `logDirectory` | `"Logs"` | Directory where log files are created |
| `batchSize` | 200 | Maximum entries flushed per write |
| `maxIdleMs` | 4000 | Maximum ms between flushes when batch is not full |
| `fullMode` | `Wait` | Channel backpressure strategy |

## Log format

```
2026-02-22 14:03:12.456 [Information] MyApp.Service: User logged in
```

## Repository

[https://github.com/alphons/VanDerHeijden.Logging](https://github.com/alphons/VanDerHeijden.Logging)
