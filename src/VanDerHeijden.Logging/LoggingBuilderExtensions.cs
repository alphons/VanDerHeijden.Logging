
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System.Threading.Channels;

namespace LoggerExtensions;

public static class LoggingBuilderExtensions
{
	public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string logDirectory = "Logs")
	{
		builder.Services.AddSingleton<ILoggerProvider>(_ =>
		{
			var logWriter = new FileLogWriter(logDirectory);
			var batchedLogger = new BatchedLogger<string>(logWriter, fullMode: BoundedChannelFullMode.Wait);
			return new BatchedLoggerProvider<string>(
				batchedLogger,
				entryFactory: (message, _) =>
					$"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}"
			);
		});
		return builder;
	}

	public static ILoggingBuilder AddMongoDbLogger(this ILoggingBuilder builder, IMongoCollection<LogEntry> collection)
	{
		builder.Services.AddSingleton<ILoggerProvider>(_ =>
		{
			var logWriter = new MongoDbLogWriter(collection);
			var batchedLogger = new BatchedLogger<LogEntry>(logWriter, batchSize: 100, maxIdleMs: 3000, fullMode: BoundedChannelFullMode.DropOldest);
			return new BatchedLoggerProvider<LogEntry>(
				batchedLogger,
				entryFactory: (message, logLevel) => new LogEntry
				{
					Timestamp = DateTime.UtcNow,
					Level = logLevel.ToString(),
					Category = message.Split(':')[0],
					Message = message
				}
			);
		});
		return builder;
	}
}