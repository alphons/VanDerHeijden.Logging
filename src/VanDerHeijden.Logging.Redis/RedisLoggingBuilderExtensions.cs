using Microsoft.AspNetCore.Http;
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
	/// <param name="configure">Optional callback to change the batching settings (<see cref="BatchedLoggerOptions"/>).</param>
	/// <returns>The <paramref name="builder"/> so that additional calls can be chained.</returns>
	public static ILoggingBuilder AddRedisLogger(
		this ILoggingBuilder builder,
		IDatabase database,
		string listKey = "logs",
		TimeSpan? ttl = null,
		Action<BatchedLoggerOptions>? configure = null)
	{
		builder.Services.AddSingleton<ILoggerProvider>(sp =>
		{
			var httpContextAccessor = sp.GetService<IHttpContextAccessor>();
			var logWriter = new RedisLogWriter(database, listKey, ttl);
			var options = new BatchedLoggerOptions { BatchSize = 200, MaxIdleMs = 2000, FullMode = BoundedChannelFullMode.DropOldest };
			configure?.Invoke(options);
			var batchedLogger = new BatchedLogger<LogEntry>(logWriter, options);
			return new BatchedLoggerProvider<LogEntry>(batchedLogger, entryFactory: e => e, httpContextAccessor);
		});
		return builder;
	}
}
