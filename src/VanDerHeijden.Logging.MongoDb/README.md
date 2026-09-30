# VanDerHeijden.Logging.MongoDb

MongoDB log writer for [VanDerHeijden.Logging](https://www.nuget.org/packages/VanDerHeijden.Logging).

Writes batched log entries to a **MongoDB collection** using an unordered `InsertManyAsync`, with structured
properties stored as a queryable BSON subdocument.

## Installation

```bash
dotnet add package VanDerHeijden.Logging.MongoDb
```

## Usage

```csharp
var mongoClient = new MongoClient("mongodb://localhost:27017");
var collection = mongoClient
    .GetDatabase("myapp")
    .GetCollection<BsonDocument>("logs");

builder.Logging.AddMongoDbLogger(collection, options => options.RetentionDays = 30);
```

Or from configuration, when an `IMongoDatabase` is registered in the DI container:

```csharp
builder.Logging.AddMongoDbLogger();
builder.Services.AddMongoDbLogging(builder.Configuration);
```

```json
{
  "MongoDb": {
    "Collections": { "Logs": "Logs" },
    "RetentionDays": 30
  }
}
```

## Options

| Option | Default | Description |
|---|---|---|
| `CreateIndexes` | `true` | Create the indexes below when the logger starts |
| `RetentionDays` | `null` | When set, entries are removed after this many days (TTL index on `Timestamp`) |
| `StoreMessage` | `true` | Store the formatted text as `Message` (`User 42 logged in`) |
| `StoreMessageTemplate` | `true` | Store the template as `MessageTemplate` (`User {UserId} logged in`) |

With both enabled a document holds the text twice: once formatted, and once as template plus `Properties`.
Choose what you need:

| `StoreMessage` | `StoreMessageTemplate` | Stored | Use when |
|---|---|---|---|
| `true` | `true` | `Message`, `MessageTemplate`, `Properties` | Default: readable text and grouping by template |
| `true` | `false` | `Message`, `Properties` | You read and search the text and do not group by template |
| `false` | `true` | `MessageTemplate`, `Properties` | Smallest documents; the viewer rebuilds the text from template and properties |
| `false` | `false` | `Message`, `Properties` | Same as `true` / `false`: the text is never dropped entirely |

`Properties` is always stored, and at least one of `Message` and `MessageTemplate` is always stored: an entry
whose template is not stored (switched off, or the log call had none) keeps its `Message` even when
`StoreMessage` is `false`.
In configuration the keys are `MongoDb:StoreMessage` and `MongoDb:StoreMessageTemplate`.

## Document

`logger.LogInformation("Order {OrderId} count {Count} amount {Amount}", orderId, 42, 12.34m)` is stored as:

```json
{
  "_id": ObjectId("..."),
  "Timestamp": ISODate("2026-02-22T14:03:12.456Z"),
  "Level": "Information",
  "EventId": 0,
  "Category": "MyApp.Service",
  "Message": "Order 3fa85f64-... count 42 amount 12.34",
  "MessageTemplate": "Order {OrderId} count {Count} amount {Amount}",
  "Properties": { "OrderId": UUID("3fa85f64-..."), "Count": 42, "Amount": NumberDecimal("12.34") },
  "Path": "/api/orders",
  "Method": "POST",
  "ClientIp": "203.0.113.42"
}
```

`EventName`, `Exception`, `Referer`, `UserAgent`, `SessionId` and `SessionGuid` are added when set; fields that are
`null` are not stored. `Timestamp` is UTC.

### Properties

The whole document, including `Properties`, is built inside `ILogger.Log` — a snapshot at log time. The driver
never receives a `Dictionary<string, object?>`, so an unusual argument type cannot make an insert fail.

| .NET value | BSON |
|---|---|
| `string`, `bool`, `int`, `long`, `double` | native |
| `DateTime`, `DateTimeOffset` | Date (UTC) |
| `decimal` | Decimal128 |
| `Guid` | Binary subtype 4 (`GuidRepresentation.Standard`) |
| enum | String (name) |
| `null` | Null |
| smaller integers, `float` | Int64 / Double |
| object, record | Subdocument (nested values mapped as JSON: string, Int32/Int64, Double, Boolean, Null) |
| collection | Array |
| not serializable, or over 32 KB of JSON | String (`ToString()`) |

Keys are sanitized: every `.` and a leading `$` become `_` (`{user.name}` is stored as `user_name`).

Query properties directly, e.g. `{ "Properties.Count": { $gt: 10 } }` or `{ "Properties.Customer.Name": "Alice" }`.

Inside an object the JSON types apply: a nested `decimal` becomes a Double and a nested `Guid` or `DateTime` a string.
Pass such a value as its own template argument when you need the native BSON type. Nested keys are sanitized too.

## Indexes

Created in the background at startup (a failure never blocks or breaks the application):

| Index | Purpose |
|---|---|
| `Timestamp` descending | Newest-first listing; carries the TTL when `RetentionDays` is set |
| `Level` + `Timestamp` descending | Filter by level |
| `Category` + `Timestamp` descending | Filter by category |
| `Properties.$**` (wildcard) | Filter on any structured property |

Changing `RetentionDays` replaces the `Timestamp` index with the new TTL.

## Reliability

- Inserts are unordered (`IsOrdered = false`): one rejected document does not stop the rest of the batch.
- `_id` is assigned when the entry is logged, so a retried batch cannot store an entry twice.
- The internal channel uses `DropOldest`: if MongoDB is unreachable, logging never blocks the application and
  the oldest buffered entries are discarded.

## HTTP fields

The HTTP fields are populated automatically when `IHttpContextAccessor` is registered:

```csharp
builder.Services.AddHttpContextAccessor();
```

Outside an HTTP context they are not stored in the document.

## Upgrading

The collection type changed from `IMongoCollection<LogEntry>` to `IMongoCollection<BsonDocument>`, and the
MongoDB-specific `LogEntry` class was removed in favour of the shared `VanDerHeijden.Logging.LogEntry`.

## Repository

[https://github.com/alphons/VanDerHeijden.Logging](https://github.com/alphons/VanDerHeijden.Logging)
