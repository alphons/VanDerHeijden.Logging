using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace VanDerHeijden.Logging.File;

/// <summary>
/// The on-disk format used by the file logger.
/// </summary>
public enum LogFormat
{
	/// <summary>Human-readable text lines in <c>log-yyyyMMdd.txt</c>.</summary>
	Text,

	/// <summary>JSON Lines (one JSON object per line) in <c>log-yyyyMMdd.jsonl</c>.</summary>
	Json
}

/// <summary>
/// Extension methods for registering file-based logging via <see cref="ILoggingBuilder"/>.
/// </summary>
public static class FileLoggingBuilderExtensions
{
	/// <summary>
	/// Adds a file logger that writes log messages to daily rotating files inside
	/// <paramref name="logDirectory"/>.
	/// </summary>
	/// <param name="builder">The <see cref="ILoggingBuilder"/> to configure.</param>
	/// <param name="logDirectory">
	/// Path to the directory where log files are written.
	/// The directory is created automatically if it does not exist.
	/// Defaults to <c>"Logs"</c>.
	/// </param>
	/// <param name="format">The file format. Defaults to <see cref="LogFormat.Text"/>.</param>
	/// <returns>The <paramref name="builder"/> so that additional calls can be chained.</returns>
	public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string logDirectory = "Logs", LogFormat format = LogFormat.Text)
	{
		builder.Services.AddSingleton<ILoggerProvider>(sp =>
		{
			var httpContextAccessor = sp.GetService<IHttpContextAccessor>();

			if (format == LogFormat.Json)
			{
				var jsonLogger = new BatchedLogger<LogEntry>(new JsonFileLogWriter(logDirectory), fullMode: BoundedChannelFullMode.Wait);
				return new BatchedLoggerProvider<LogEntry>(jsonLogger, entryFactory: e => e, httpContextAccessor);
			}

			var logWriter = new FileLogWriter(logDirectory);
			var batchedLogger = new BatchedLogger<string>(logWriter, fullMode: BoundedChannelFullMode.Wait);
			return new BatchedLoggerProvider<string>(batchedLogger, FormatText, httpContextAccessor);
		});
		return builder;
	}

	internal static string FormatText(LogEntry e)
	{
		var http = e.Method is null ? "" : $" [{e.Method} {e.Path} {e.ClientIp} {e.SessionId} {e.SessionGuid}]";
		var ex = e.Exception is null ? "" : $"{Environment.NewLine}{e.Exception}";
		return $"{e.Timestamp:yyyy-MM-dd HH:mm:ss.fff}Z [{e.Level}]{http} [{e.Category}] {e.Message}{ex}{Environment.NewLine}";
	}
}
