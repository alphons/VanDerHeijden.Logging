using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace VanDerHeijden.Logging;

public static class FileLoggingBuilderExtensions
{
	public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string logDirectory = "Logs")
	{
		builder.Services.AddSingleton<ILoggerProvider>(_ =>
		{
			var logWriter = new FileLogWriter(logDirectory);
			var batchedLogger = new BatchedLogger<string>(logWriter, fullMode: BoundedChannelFullMode.Wait);
			return new BatchedLoggerProvider<string>(
				batchedLogger,
				entryFactory: (message, _) =>
					$"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}"
			);
		});
		return builder;
	}
}
