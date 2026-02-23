using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace VanDerHeijden.Logging;

/// <summary>
/// Adding SimpleConsole logging and set DefaultLogLevel
/// </summary>
public static class CustomLoggingExtensions
{
	/// <summary>
	/// 
	/// </summary>
	/// <param name="services"></param>
	/// <param name="ConsoleLogging"></param>
	/// <param name="DefaultLogLevel">Trace, Debug, Information, Warning, Error, Critical, None</param>
	/// <returns>The <paramref name="services"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddCustomLogging(this IServiceCollection services, bool ConsoleLogging = true, string DefaultLogLevel = "Information")
	{
		services.AddLogging(logging =>
		{
			logging.ClearProviders();
			logging.AddFilter("Microsoft.AspNetCore.Watch", LogLevel.None);
			logging.AddFilter("Microsoft.AspNetCore.Watch.BrowserRefresh", LogLevel.None);
			if (ConsoleLogging)
			{
				logging.AddSimpleConsole(c =>
				{
					c.SingleLine = true;
					c.TimestampFormat = "HH:mm:ss ";
				});
			}
			LogLevel minimumLevel = Enum.TryParse<LogLevel>(DefaultLogLevel, true, out LogLevel parsedLevel) ? parsedLevel : LogLevel.Information;
			logging.SetMinimumLevel(minimumLevel);
		});
		return services;
	}
}