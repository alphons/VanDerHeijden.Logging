using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace VanDerHeijden.Logging.Sql;

public static class SqlLoggingBuilderExtensions
{
	public static ILoggingBuilder AddSqlLogger(
		this ILoggingBuilder builder,
		string connectionString,
		string tableName = "Logs")
	{
		builder.Services.AddSingleton<ILoggerProvider>(_ =>
		{
			var logWriter = new SqlLogWriter(connectionString, tableName);
			var batchedLogger = new BatchedLogger<SqlLogEntry>(logWriter, batchSize: 200, maxIdleMs: 4000, fullMode: BoundedChannelFullMode.Wait);
			return new BatchedLoggerProvider<SqlLogEntry>(
				batchedLogger,
				entryFactory: (message, logLevel) => new SqlLogEntry
				{
					Timestamp = DateTime.UtcNow,
					Level     = logLevel.ToString(),
					Category  = message.Split(':')[0],
					Message   = message
				}
			);
		});
		return builder;
	}
}
