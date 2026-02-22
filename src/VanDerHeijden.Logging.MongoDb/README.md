# VanDerHeijden.Logging.MongoDb

MongoDB log writer for [VanDerHeijden.Logging](https://www.nuget.org/packages/VanDerHeijden.Logging).

Writes batched log entries to a **MongoDB collection** using `InsertManyAsync`.

## Installation

```bash
dotnet add package VanDerHeijden.Logging.MongoDb
```

## Usage

```csharp
var mongoClient = new MongoClient("mongodb://localhost:27017");
var collection = mongoClient
    .GetDatabase("myapp")
    .GetCollection<LogEntry>("logs");

builder.Logging.AddMongoDbLogger(collection);
```

## Log entry schema

```csharp
public class LogEntry
{
    public string Id        { get; set; }  // ObjectId
    public DateTime Timestamp { get; set; }
    public string Level     { get; set; }
    public string Category  { get; set; }
    public string Message   { get; set; }
    public string? Exception { get; set; }
}
```

## Repository

[https://github.com/alphons/VanDerHeijden.Logging](https://github.com/alphons/VanDerHeijden.Logging)
