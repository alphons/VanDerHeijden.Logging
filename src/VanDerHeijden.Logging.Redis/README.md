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
  "eventId": 0,
  "category": "MyApp.Service",
  "message": "User 42 logged in",
  "messageTemplate": "User {UserId} logged in",
  "properties": { "UserId": 42 },
  "path": "/api/users/login",
  "method": "POST",
  "clientIp": "203.0.113.42",
  "referer": "https://example.com/login",
  "userAgent": "Mozilla/5.0 ..."
}
```

This is the shared `VanDerHeijden.Logging.LogEntry` (`RedisLogEntry` has been removed), serialized by the shared
`LogEntryJsonWriter` — the same field names as the JSON file logger. `timestamp` is always UTC. Fields that are
`null` (`eventName`, `exception`, `sessionId`, `sessionGuid`, ...) are omitted.

The HTTP fields are populated automatically when `IHttpContextAccessor` is registered:

```csharp
builder.Services.AddHttpContextAccessor();
```

Outside an HTTP context they are omitted.

## Notes

- The Redis list grows until consumed. Make sure a consumer drains it via `BLPOP`/`LPOP`.
- Use `ttl` to automatically expire the key if no consumer is configured.
- `FullMode` defaults to `DropOldest` — under extreme load, oldest log entries are dropped to protect application throughput.
- Batching is configurable: `AddRedisLogger(db, configure: options => options.BatchSize = 500)`. Defaults: `BatchSize` 200, `MaxIdleMs` 2000, `QueueCapacity` 10 000.

## Repository

[https://github.com/alphons/VanDerHeijden.Logging](https://github.com/alphons/VanDerHeijden.Logging)
