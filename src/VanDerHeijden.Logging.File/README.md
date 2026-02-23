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
