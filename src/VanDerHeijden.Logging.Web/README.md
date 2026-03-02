# VanDerHeijden.Logging

High-performance, low-allocation batched logging for .NET 10, built on Microsoft.Extensions.Logging.  
Log entries are enqueued to an in-memory Channel<T> and flushed asynchronously in configurable batches. No I/O occurs on the hot path (application code).

Supports pluggable writers:  
- Daily rotating text files  
- MongoDB collections  
- SQL Server (SqlBulkCopy)  
- Redis lists (RPUSH)  
- Custom implementations

**VanDerHeijden.Logging.Web** adds HTTP-context extensions for structured request logging.

## Packages

| Package                          | Description                                      | NuGet Link |
|----------------------------------|--------------------------------------------------|------------|
| VanDerHeijden.Logging            | Core abstractions & batched logger               | [NuGet](https://www.nuget.org/packages/VanDerHeijden.Logging) |
| VanDerHeijden.Logging.File       | Daily rotating text file writer                  | (separate package) |
| VanDerHeijden.Logging.Web        | ILogger extensions with HttpContext scope        | (separate package) |
| VanDerHeijden.Logging.MongoDb    | MongoDB collection writer                        | (separate) |
| VanDerHeijden.Logging.Sql        | SQL Server bulk insert writer                    | (separate) |
| VanDerHeijden.Logging.Redis      | Redis list writer                                | (separate) |

## Installation

```bash
dotnet add package VanDerHeijden.Logging
dotnet add package VanDerHeijden.Logging.File     # recommended text file sink
dotnet add package VanDerHeijden.Logging.Web      # for web request context logging
```

## Core Setup (Program.cs)

```csharp
using VanDerHeijden.Logging.File;

builder.Logging.AddBatchedFileLogger(options =>
{
    options.LogDirectory        = "Logs";
    options.BatchSize           = 200;
    options.MaxIdleMilliseconds = 4000;
    options.FullMode            = BatchFullMode.DropOldest; // or Wait
    // Optional: custom file naming, buffer size, etc.
});
```

## Web Logging Extensions Usage

In controllers, services, endpoints:

```csharp
using Microsoft.AspNetCore.Mvc;
using VanDerHeijden.Logging.Web;

[ApiController]
[Route("api/orders")]
public class OrdersController(ILogger<OrdersController> logger) : ControllerBase
{
    [HttpPost]
    public IActionResult Create(OrderDto dto)
    {
        try
        {
            // business logic...
            logger.LogInformationWithContext(
                HttpContext,
                "Order created {OrderId} by {User}",
                12345,
                dto.UserId);

            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogErrorWithContext(
                HttpContext,
                "Failed to create order {OrderId}",
                dto.Id);

            return StatusCode(500);
        }
    }
}
```

Available extensions (all add structured scope):

- `LogInformationWithContext(HttpContext?, string, params object?[])`
- `LogWarningWithContext`
- `LogErrorWithContext`
- `LogDebugWithContext`

Scope properties added (if HttpContext provided):

- TraceId
- RequestMethod
- RequestUrl (full scheme/host/path/query)
- ClientIp (X-Forwarded-For aware)
- Referer
- UserAgent
- User (from ClaimsPrincipal)
- SessionId (if session enabled)

Falls back to plain log if context is null.

## Features

- Zero I/O / low-allocation on Log() calls
- Configurable batch size & idle timeout
- Background flush consumer
- Daily file rotation (no restart needed)
- Thread-safe, proxy-friendly (X-Forwarded-For)
- Structured properties for sinks like Seq, ELK, etc.

## Repository

https://github.com/alphons/VanDerHeijden.Logging

MIT license. Issues / PRs welcome.
