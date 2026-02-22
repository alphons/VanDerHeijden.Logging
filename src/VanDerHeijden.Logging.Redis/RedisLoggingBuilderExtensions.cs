using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Threading.Channels;

namespace VanDerHeijden.Logging;

public static class RedisLoggingBuilderExtensions
{
	public static ILoggingBuilder AddRedisLogger(
		this ILoggingBuilder builder,
		IDatabase database,
		string listKey = "logs",
		TimeSpan? ttl = null)
	{
		builder.Services.AddSingleton<ILoggerProvider>(_ =>
		{
			var logWriter = new RedisLogWriter(database, listKey, ttl);
			var batchedLogger = new BatchedLogger<RedisLogEntry>(logWriter, batchSize: 200, maxIdleMs: 2000, fullMode: BoundedChannelFullMode.DropOldest);
			return new BatchedLoggerProvider<RedisLogEntry>(
				batchedLogger,
				entryFactory: (message, logLevel) => new RedisLogEntry
				{
					Timestamp = DateTime.UtcNow,
					Level     = logLevel.ToString(),
					Category  = message.Split(':')[0],
					Message   = message
				}
			);
		});
		return builder;
	}
}
