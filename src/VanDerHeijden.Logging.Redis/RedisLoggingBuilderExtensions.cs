using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Threading.Channels;

namespace VanDerHeijden.Logging.Redis;

/// <summary>
/// Extension methods for registering Redis-based logging via <see cref="ILoggingBuilder"/>.
/// </summary>
public static class RedisLoggingBuilderExtensions
{
	/// <summary>
	/// Adds a Redis logger that pushes log entries as JSON to a Redis list using <c>RPUSH</c>.
	/// </summary>
	/// <param name="builder">The <see cref="ILoggingBuilder"/> to configure.</param>
	/// <param name="database">The Redis database instance used for all write operations.</param>
	/// <param name="listKey">The Redis key of the list that receives log entries. Defaults to <c>"logs"</c>.</param>
	/// <param name="ttl">
	/// Optional time-to-live applied to <paramref name="listKey"/> after each batch write.
	/// When <see langword="null"/> (the default) the key never expires.
	/// </param>
	/// <returns>The <paramref name="builder"/> so that additional calls can be chained.</returns>
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
