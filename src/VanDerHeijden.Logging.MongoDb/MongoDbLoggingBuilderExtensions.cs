using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System.Threading.Channels;

namespace VanDerHeijden.Logging.MongoDb;

/// <summary>
/// Extension methods for registering MongoDB-based logging via <see cref="ILoggingBuilder"/>.
/// </summary>
public static class MongoDbLoggingBuilderExtensions
{
	/// <summary>
	/// Adds a MongoDB logger that inserts log entries into the specified collection in batches.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to configure.</param>
	/// <param name="collection">The MongoDB collection that will receive <see cref="LogEntry"/> documents.</param>
	/// <returns>The <paramref name="services"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddMongoDbLogger(this IServiceCollection services, IMongoCollection<LogEntry> collection)
	{
		services.AddSingleton<ILoggerProvider>(_ =>
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
		return services;
	}
}
