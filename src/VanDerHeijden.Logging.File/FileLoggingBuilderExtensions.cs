using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace VanDerHeijden.Logging.File;

/// <summary>
/// Extension methods for registering file-based logging via <see cref="ILoggingBuilder"/>.
/// </summary>
public static class FileLoggingBuilderExtensions
{
	/// <summary>
	/// Adds a file logger that writes log messages to daily rotating text files inside
	/// <paramref name="logDirectory"/>.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to configure.</param>
	/// <param name="logDirectory">
	/// Path to the directory where log files are written.
	/// The directory is created automatically if it does not exist.
	/// Defaults to <c>"Logs"</c>.
	/// </param>
	/// <returns>The <paramref name="services"/> so that additional calls can be chained.</returns>
	public static IServiceCollection AddFileLogger(this IServiceCollection services, string logDirectory = "Logs")
	{
		services.AddSingleton<ILoggerProvider>(_ =>
		{
			var logWriter = new FileLogWriter(logDirectory);
			var batchedLogger = new BatchedLogger<string>(logWriter, fullMode: BoundedChannelFullMode.Wait);
			return new BatchedLoggerProvider<string>(
				batchedLogger,
				entryFactory: (message, _) =>
					$"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}"
			);
		});
		return services;
	}
}
