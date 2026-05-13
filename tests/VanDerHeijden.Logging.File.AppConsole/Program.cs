
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VanDerHeijden.Logging;
using VanDerHeijden.Logging.File;

string APPSETTINGS_FILE = "appsettings.json";

IConfigurationRoot configuration = new ConfigurationBuilder()
			.SetBasePath(Directory.GetCurrentDirectory())
			.AddJsonFile(APPSETTINGS_FILE, optional: false, reloadOnChange: true)
			.Build();

IConfigurationSection configurationSection = configuration.GetSection("CustomLogging");
using var loggerFactory = LoggerFactory.Create(b =>
{
	if (configurationSection.GetValue<bool>("ConsoleLogging"))
		b.AddCustomLogger();
	if (configurationSection.GetValue<bool>("FileLogging"))
		b.AddFileLogger();
});

var logger = loggerFactory.CreateLogger<string>();

logger.LogInformation("Fast logging");