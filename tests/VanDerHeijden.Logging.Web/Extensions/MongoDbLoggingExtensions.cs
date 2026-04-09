using MongoDB.Driver;
using VanDerHeijden.Logging.MongoDb;
using VanDerHeijden.Logging.Web.Extensions;

namespace VanDerHeijden.Logging.Web.Extensions;

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

