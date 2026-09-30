using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
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
	/// <param name="collection">The MongoDB collection that will receive the log documents.</param>
	/// <param name="configure">Optional callback to configure <see cref="MongoDbLoggerOptions"/>.</param>
	/// <returns>The <paramref name="builder"/> so that additional calls can be chained.</returns>
	public static ILoggingBuilder AddMongoDbLogger(this ILoggingBuilder builder, IMongoCollection<BsonDocument> collection, Action<MongoDbLoggerOptions>? configure = null) =>
		builder.AddMongoDbLogger(sp => collection, configure);

	/// <summary>
	/// Adds a MongoDB logger that resolves the collection from the DI container at startup.
	/// </summary>
	/// <param name="builder">The <see cref="ILoggingBuilder"/> to configure.</param>
	/// <param name="collectionFactory">
	/// A factory that receives the <see cref="IServiceProvider"/> and returns the
	/// <see cref="IMongoCollection{TDocument}"/> to write log documents to.
	/// </param>
	/// <param name="configure">Optional callback to configure <see cref="MongoDbLoggerOptions"/>.</param>
	/// <returns>The <paramref name="builder"/> so that additional calls can be chained.</returns>
	public static ILoggingBuilder AddMongoDbLogger(this ILoggingBuilder builder, Func<IServiceProvider, IMongoCollection<BsonDocument>> collectionFactory, Action<MongoDbLoggerOptions>? configure = null)
	{
		builder.Services.AddSingleton<ILoggerProvider>(sp =>
		{
			var options = new MongoDbLoggerOptions();
			configure?.Invoke(options);
			return CreateProvider(sp, collectionFactory(sp), options);
		});
		return builder;
	}

	internal static ILoggerProvider CreateProvider(IServiceProvider sp, IMongoCollection<BsonDocument> collection, MongoDbLoggerOptions options)
	{
		if (options.CreateIndexes)
		{
			// Runs in the background: an unreachable server must not block or break application startup.
			Task.Run(async () =>
			{
				try { await MongoDbLogIndexes.EnsureAsync(collection, options); } catch { }
			});
		}

		var httpContextAccessor = sp.GetService<IHttpContextAccessor>();
		var logWriter = new MongoDbLogWriter(collection, options);
		var batchedLogger = new BatchedLogger<LogEntry>(logWriter, batchSize: 100, maxIdleMs: 3000, fullMode: BoundedChannelFullMode.DropOldest);

		// Property values are snapshotted in ILogger.Log; the BSON document is built later by the writer.
		return new BatchedLoggerProvider<LogEntry>(batchedLogger, entryFactory: entry => entry, httpContextAccessor);
	}
}
