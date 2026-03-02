# VanDerHeijden.Logging.Web

Web log writer for [VanDerHeijden.Logging](https://www.nuget.org/packages/VanDerHeijden.Logging).

Writes batched Web log entries to **daily rotating text files** using async file I/O with a 64 KB write buffer. A new file is opened automatically at midnight without restarting the application.

## Installation

```bash
dotnet add package VanDerHeijden.Logging.Web
```

## Usage

```csharp
builder.Logging.AddFileLogger(logDirectory: "Logs");
```



## Repository

[https://github.com/alphons/VanDerHeijden.Logging](https://github.com/alphons/VanDerHeijden.Logging)
