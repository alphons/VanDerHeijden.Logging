using MongoDB.Driver;

namespace VanDerHeijden.Logging.Web.Extensions;

public static class MongoDbDatabaseExtensions
{
	public static IServiceCollection AddMongoDbDatabase(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddSingleton<IMongoClient>(sp =>
		{
			var settings = MongoClientSettings.FromConnectionString(configuration["ConnectionStrings:MongoDb"]);
			settings.RetryWrites = true;
			settings.RetryReads = true;
			return new MongoClient(settings);
		});

		services.AddSingleton<IMongoDatabase>(sp =>
		{
			var client = sp.GetRequiredService<IMongoClient>();
			return client.GetDatabase(configuration["MongoDb:DatabaseName"]);
		});

		return services;
	}
}
