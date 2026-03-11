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

## Required table schema

```sql
CREATE TABLE Logs (
    Id        BIGINT IDENTITY PRIMARY KEY,
    Timestamp DATETIME2       NOT NULL,
    Level     NVARCHAR(20)    NOT NULL,
    Category  NVARCHAR(256)   NOT NULL,
    Message   NVARCHAR(MAX)   NOT NULL,
    Exception NVARCHAR(MAX)   NULL,
    Path      NVARCHAR(1024)  NULL,
    Method    NVARCHAR(10)    NULL,
    ClientIp  NVARCHAR(45)    NULL,
    Referer   NVARCHAR(2048)  NULL,
    UserAgent NVARCHAR(512)   NULL
);
```

The HTTP columns (`Path`, `Method`, `ClientIp`, `Referer`, `UserAgent`) are populated automatically when
`IHttpContextAccessor` is registered in the DI container:

```csharp
builder.Services.AddHttpContextAccessor();
```

Outside an HTTP context (background services, console apps) these columns are `NULL`.

### Migrating an existing table

```sql
ALTER TABLE Logs
    ADD Path      NVARCHAR(1024) NULL,
        Method    NVARCHAR(10)   NULL,
        ClientIp  NVARCHAR(45)   NULL,
        Referer   NVARCHAR(2048) NULL,
        UserAgent NVARCHAR(512)  NULL;
```

## Repository

[https://github.com/alphons/VanDerHeijden.Logging](https://github.com/alphons/VanDerHeijden.Logging)
