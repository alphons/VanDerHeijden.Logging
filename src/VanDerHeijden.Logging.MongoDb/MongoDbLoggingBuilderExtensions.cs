using Microsoft.AspNetCore.Http;
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
	/// <param name="builder">The <see cref="ILoggingBuilder"/> to configure.</param>
	/// <param name="collection">The MongoDB collection that will receive <see cref="LogEntry"/> documents.</param>
	/// <returns>The <paramref name="builder"/> so that additional calls can be chained.</returns>
	public static ILoggingBuilder AddMongoDbLogger(this ILoggingBuilder builder, IMongoCollection<LogEntry> collection) =>
		builder.AddMongoDbLogger(_ => collection);

	/// <summary>
	/// Adds a MongoDB logger that resolves the collection from the DI container at startup.
	/// Use this overload when <see cref="IMongoCollection{TDocument}"/> is already registered as a service.
	/// </summary>
	/// <param name="builder">The <see cref="ILoggingBuilder"/> to configure.</param>
	/// <param name="collectionFactory">
	/// A factory that receives the <see cref="IServiceProvider"/> and returns the
	/// <see cref="IMongoCollection{TDocument}"/> to write log entries to.
	/// </param>
	/// <returns>The <paramref name="builder"/> so that additional calls can be chained.</returns>
	public static ILoggingBuilder AddMongoDbLogger(this ILoggingBuilder builder, Func<IServiceProvider, IMongoCollection<LogEntry>> collectionFactory)
	{
		builder.Services.AddSingleton<ILoggerProvider>(sp =>
		{
			var httpContextAccessor = sp.GetService<IHttpContextAccessor>();
			var logWriter = new MongoDbLogWriter(collectionFactory(sp));
			var batchedLogger = new BatchedLogger<LogEntry>(logWriter, batchSize: 100, maxIdleMs: 3000, fullMode: BoundedChannelFullMode.DropOldest);
			return new BatchedLoggerProvider<LogEntry>(
				batchedLogger,
				entryFactory: (message, logLevel, ctx, exception) => new LogEntry
				{
					Timestamp = DateTime.UtcNow,
					Level     = logLevel.ToString(),
					Category  = message.Split(':')[0],
					Message   = message,
					Exception = exception?.ToString(),
					Path      = ctx?.Path,
					Method    = ctx?.Method,
					ClientIp  = ctx?.ClientIp,
					Referer   = ctx?.Referer,
					UserAgent = ctx?.UserAgent
				},
				httpContextAccessor
			);
		});
		return builder;
	}
}
