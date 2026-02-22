# VanDerHeijden.Logging.Redis

Redis log writer for [VanDerHeijden.Logging](https://www.nuget.org/packages/VanDerHeijden.Logging).

Writes batched log entries to a **Redis list** using `RPUSH`. Each entry is serialized as JSON. The list can be consumed by any Redis-compatible consumer such as Logstash, a worker service, or a custom processor via `BLPOP`.

## Installation

```bash
dotnet add package VanDerHeijden.Logging.Redis
```

## Usage

```csharp
var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
var db = redis.GetDatabase();

builder.Logging.AddRedisLogger(
    database: db,
    listKey: "logs",
    ttl: TimeSpan.FromDays(7));   // optional: auto-expire the key
```

## Log entry format (JSON)

```json
{
  "timestamp": "2026-02-22T14:03:12.456Z",
  "level": "Information",
  "category": "MyApp.Service",
  "message": "MyApp.Service: User logged in",
  "exception": null
}
```

## Notes

- The Redis list grows until consumed. Make sure a consumer drains it via `BLPOP`/`LPOP`.
- Use `ttl` to automatically expire the key if no consumer is configured.
- `fullMode: DropOldest` is the default — under extreme load, oldest log entries are dropped to protect application throughput.

## Repository

[https://github.com/alphons/VanDerHeijden.Logging](https://github.com/alphons/VanDerHeijden.Logging)
