using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace VanDerHeijden.Logging;

/// <summary>
/// Adding SimpleConsole logging and set DefaultLogLevel
/// </summary>
public static class CustomLoggingExtensions
{
	/// <summary>
	/// AddCustomLogging for SimpleConsole and setting DefaultLogLevel via <see cref="ILoggingBuilder" />
	/// </summary>
	///  <param name="builder">The <see cref="ILoggingBuilder"/> to configure.</param>
	/// <param name="ConsoleLogging">true for SimpleConsole logging</param>
	/// <param name="DefaultLogLevel">Trace, Debug, Information, Warning, Error, Critical, None</param>
	/// <returns>The <paramref name="builder"/> so that additional calls can be chained.</returns>
	public static ILoggingBuilder AddCustomLogging(this ILoggingBuilder builder, bool ConsoleLogging = true, string DefaultLogLevel = "Information")
	{
		builder.Services.AddLogging(logging =>
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
		return builder;
	}
}