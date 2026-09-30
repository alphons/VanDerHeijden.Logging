using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;


namespace VanDerHeijden.Logging.MongoDb;

/// <summary>
/// The log collection and options registered by <see cref="MongoDbLoggingExtensions.AddMongoDbLogging"/>.
/// </summary>
/// <param name="Collection">The collection that receives the log documents.</param>
/// <param name="Options">The logger options read from configuration.</param>
public sealed record MongoDbLogTarget(IMongoCollection<BsonDocument> Collection, MongoDbLoggerOptions Options);

/// <summary>
/// Configuration-based registration of the MongoDB logger.
/// </summary>
public static class MongoDbLoggingExtensions
{
	/// <summary>
	/// Registers the log collection using the <see cref="IMongoDatabase"/> from the DI container.
	/// Reads <c>MongoDb:Collections:Logs</c> (collection name, default <c>"Logs"</c>) and
	/// <c>MongoDb:RetentionDays</c> (optional TTL in days), <c>MongoDb:StoreMessage</c> and
	/// <c>MongoDb:StoreMessageTemplate</c> (both default <c>true</c>), and the batching settings
	/// <c>MongoDb:BatchSize</c>, <c>MongoDb:MaxIdleMs</c> and <c>MongoDb:QueueCapacity</c> from <paramref name="configuration"/>.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The application configuration.</param>
	/// <returns>The <paramref name="services"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddMongoDbLogging(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddHttpContextAccessor();

		services.AddSingleton(sp =>
		{
			IMongoDatabase database = sp.GetRequiredService<IMongoDatabase>();
			string collectioname = configuration["MongoDb:Collections:Logs"] ?? "Logs";
			var options = new MongoDbLoggerOptions
			{
				RetentionDays = configuration.GetValue<int?>("MongoDb:RetentionDays"),
				StoreMessage = configuration.GetValue("MongoDb:StoreMessage", true),
				StoreMessageTemplate = configuration.GetValue("MongoDb:StoreMessageTemplate", true)
			};
			options.BatchSize = configuration.GetValue("MongoDb:BatchSize", options.BatchSize);
			options.MaxIdleMs = configuration.GetValue("MongoDb:MaxIdleMs", options.MaxIdleMs);
			options.QueueCapacity = configuration.GetValue("MongoDb:QueueCapacity", options.QueueCapacity);
			return new MongoDbLogTarget(database.GetCollection<BsonDocument>(collectioname), options);
		});

		return services;
	}

	/// <summary>
	/// Adds the MongoDB logger using the collection registered by <see cref="AddMongoDbLogging"/>.
	/// </summary>
	/// <param name="logging">The <see cref="ILoggingBuilder"/> to configure.</param>
	/// <returns>The <paramref name="logging"/> builder so that additional calls can be chained.</returns>
	public static ILoggingBuilder AddMongoDbLogger(this ILoggingBuilder logging)
	{
		logging.Services.AddSingleton<ILoggerProvider>(sp =>
		{
			var target = sp.GetRequiredService<MongoDbLogTarget>();
			return MongoDbLoggingBuilderExtensions.CreateProvider(sp, target.Collection, target.Options);
		});
		return logging;
	}
}
