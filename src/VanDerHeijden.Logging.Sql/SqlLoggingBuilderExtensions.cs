using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace VanDerHeijden.Logging.Sql;

/// <summary>
/// Extension methods for registering SQL Server-based logging via <see cref="ILoggingBuilder"/>.
/// </summary>
public static class SqlLoggingBuilderExtensions
{
	/// <summary>
	/// Adds a SQL Server logger that bulk-inserts log entries into the specified table using <c>SqlBulkCopy</c>.
	/// </summary>
	/// <param name="builder">The <see cref="ILoggingBuilder"/> to configure.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="tableName">
	/// The destination table name. Defaults to <c>"Logs"</c>.
	/// See <see cref="SqlLogWriter"/> for the expected schema.
	/// </param>
	/// <returns>The <paramref name="builder"/> so that additional calls can be chained.</returns>
	public static ILoggingBuilder AddSqlLogger(
		this ILoggingBuilder builder,
		string connectionString,
		string tableName = "Logs")
	{
		builder.Services.AddSingleton<ILoggerProvider>(sp =>
		{
			var httpContextAccessor = sp.GetService<IHttpContextAccessor>();
			var logWriter = new SqlLogWriter(connectionString, tableName);
			var batchedLogger = new BatchedLogger<LogEntry>(logWriter, batchSize: 200, maxIdleMs: 4000, fullMode: BoundedChannelFullMode.Wait);
			return new BatchedLoggerProvider<LogEntry>(batchedLogger, entryFactory: e => e, httpContextAccessor);
		});
		return builder;
	}
}
