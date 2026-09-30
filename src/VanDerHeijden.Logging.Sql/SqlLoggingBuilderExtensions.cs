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
	/// <param name="configure">Optional callback to change the batching settings (<see cref="BatchedLoggerOptions"/>).</param>
	/// <returns>The <paramref name="builder"/> so that additional calls can be chained.</returns>
	public static ILoggingBuilder AddSqlLogger(
		this ILoggingBuilder builder,
		string connectionString,
		string tableName = "Logs",
		Action<BatchedLoggerOptions>? configure = null)
	{
		builder.Services.AddSingleton<ILoggerProvider>(sp =>
		{
			var httpContextAccessor = sp.GetService<IHttpContextAccessor>();
			var logWriter = new SqlLogWriter(connectionString, tableName);
			var options = new BatchedLoggerOptions { BatchSize = 200, MaxIdleMs = 4000, FullMode = BoundedChannelFullMode.Wait };
			configure?.Invoke(options);
			var batchedLogger = new BatchedLogger<LogEntry>(logWriter, options);
			return new BatchedLoggerProvider<LogEntry>(batchedLogger, entryFactory: e => e, httpContextAccessor);
		});
		return builder;
	}
}
