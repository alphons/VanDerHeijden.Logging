using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;


namespace VanDerHeijden.Logging.MongoDb;

public static class MongoDbLoggingExtensions
{
	public static IServiceCollection AddMongoDbLogging(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddHttpContextAccessor();

		services.AddSingleton<IMongoCollection<LogEntry>>(sp =>
		{
			IMongoDatabase database = sp.GetRequiredService<IMongoDatabase>();
			string collectioname = configuration["MongoDb:Collections:Logs"] ?? "Logs";
			return database.GetCollection<LogEntry>(collectioname);
		});

		return services;
	}

	public static ILoggingBuilder AddMongoDbLogger(this ILoggingBuilder logging) =>
		logging.AddMongoDbLogger(sp => sp.GetRequiredService<IMongoCollection<LogEntry>>());
}

