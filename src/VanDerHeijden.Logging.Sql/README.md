# VanDerHeijden.Logging.Sql

SQL Server log writer for [VanDerHeijden.Logging](https://www.nuget.org/packages/VanDerHeijden.Logging).

Writes batched log entries to a **SQL Server table** using `SqlBulkCopy` for high-throughput inserts.

## Installation

```bash
dotnet add package VanDerHeijden.Logging.Sql
```

## Usage

```csharp
builder.Logging.AddSqlLogger(
    connectionString: "Server=.;Database=MyApp;Integrated Security=true;",
    tableName: "Logs");
```

Batching is configurable: `AddSqlLogger(connectionString, configure: options => options.BatchSize = 500)`.
Defaults: `BatchSize` 200, `MaxIdleMs` 4000, `QueueCapacity` 10 000, `FullMode` `Wait` (logging blocks when the queue is full; nothing is dropped).

## Required table schema

```sql
CREATE TABLE Logs (
    Id              BIGINT IDENTITY PRIMARY KEY,
    Timestamp       DATETIME2       NOT NULL,  -- UTC
    Level           NVARCHAR(20)    NOT NULL,
    EventId         INT             NOT NULL,
    EventName       NVARCHAR(256)   NULL,
    Category        NVARCHAR(256)   NOT NULL,
    Message         NVARCHAR(MAX)   NOT NULL,
    MessageTemplate NVARCHAR(MAX)   NULL,
    Properties      NVARCHAR(MAX)   NULL,      -- JSON object
    Exception       NVARCHAR(MAX)   NULL,
    Path            NVARCHAR(1024)  NULL,
    Method          NVARCHAR(10)    NULL,
    ClientIp        NVARCHAR(45)    NULL,
    Referer         NVARCHAR(2048)  NULL,
    UserAgent       NVARCHAR(512)   NULL,
    SessionId       NVARCHAR(256)   NULL,
    SessionGuid     NVARCHAR(36)    NULL
);
```

`Timestamp` is always UTC. `Properties` holds the structured log properties as a JSON object
(e.g. `{"UserId":42}` for `logger.LogInformation("User {UserId} logged in", 42)`), and `MessageTemplate`
the original template; query them with `JSON_VALUE(Properties, '$.UserId')`.

The HTTP columns (`Path`, `Method`, `ClientIp`, `Referer`, `UserAgent`, `SessionId`, `SessionGuid`) are populated automatically when
`IHttpContextAccessor` is registered in the DI container:

```csharp
builder.Services.AddHttpContextAccessor();
```

Outside an HTTP context (background services, console apps) these columns are `NULL`.

### Migrating an existing table

The writer maps every column listed above, so a table created for an earlier version must be extended
**before** upgrading — otherwise the bulk insert fails and the entries are lost.

```sql
ALTER TABLE Logs
    ADD EventId         INT           NOT NULL DEFAULT 0,
        EventName       NVARCHAR(256) NULL,
        MessageTemplate NVARCHAR(MAX) NULL,
        Properties      NVARCHAR(MAX) NULL;
```

The entry type is now the shared `VanDerHeijden.Logging.LogEntry`; `SqlLogEntry` has been removed.

## Repository

[https://github.com/alphons/VanDerHeijden.Logging](https://github.com/alphons/VanDerHeijden.Logging)
