using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System.Threading.Channels;

namespace VanDerHeijden.Logging;

public static class MongoDbLoggingBuilderExtensions
{
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
