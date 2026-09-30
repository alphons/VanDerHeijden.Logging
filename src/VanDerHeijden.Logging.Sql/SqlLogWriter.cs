using Microsoft.Data.SqlClient;

namespace VanDerHeijden.Logging.Sql;

/// <summary>
/// Writes log entries to a SQL Server table in bulk using SqlBulkCopy.
/// Expected table schema:
///   CREATE TABLE Logs (
///     Id              BIGINT IDENTITY PRIMARY KEY,
///     Timestamp       DATETIME2       NOT NULL,
///     Level           NVARCHAR(20)    NOT NULL,
///     EventId         INT             NOT NULL,
///     EventName       NVARCHAR(256)   NULL,
///     Category        NVARCHAR(256)   NOT NULL,
///     Message         NVARCHAR(MAX)   NOT NULL,
///     MessageTemplate NVARCHAR(MAX)   NULL,
///     Properties      NVARCHAR(MAX)   NULL,
///     Exception       NVARCHAR(MAX)   NULL,
///     Path            NVARCHAR(1024)  NULL,
///     Method          NVARCHAR(10)    NULL,
///     ClientIp        NVARCHAR(45)    NULL,
///     Referer         NVARCHAR(2048)  NULL,
///     UserAgent       NVARCHAR(512)   NULL,
///     SessionId       NVARCHAR(256)   NULL,
///     SessionGuid     NVARCHAR(36)    NULL
///   );
/// <c>Timestamp</c> is UTC; <c>Properties</c> holds the structured properties as a JSON object.
/// </summary>
public sealed class SqlLogWriter(string connectionString, string tableName = "Logs") : IBatchedLogWriter<LogEntry>
{
	private static readonly string[] Columns =
	[
		"Timestamp", "Level", "EventId", "EventName", "Category", "Message", "MessageTemplate", "Properties",
		"Exception", "Path", "Method", "ClientIp", "Referer", "UserAgent", "SessionId", "SessionGuid"
	];

	/// <summary>
	/// Bulk-inserts all entries into the configured SQL Server table using <see cref="SqlBulkCopy"/>.
	/// </summary>
	/// <param name="entries">The log entries to insert.</param>
	/// <param name="ct">A token that can cancel the operation.</param>
	public async Task WriteBatchAsync(List<LogEntry> entries, CancellationToken ct)
	{
		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(ct);

		using var bulkCopy = new SqlBulkCopy(connection)
		{
			DestinationTableName = tableName,
			BulkCopyTimeout = 30
		};

		foreach (var column in Columns)
			bulkCopy.ColumnMappings.Add(column, column);

		var table = ToDataTable(entries);
		await bulkCopy.WriteToServerAsync(table, ct);
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	private readonly LogEntryJsonWriter json = new();

	private object PropertiesJson(LogEntry e)
	{
		if (e.Properties is not { Count: > 0 } properties) return DBNull.Value;
		json.Clear();
		json.WriteProperties(properties);
		return json.ToString();
	}

	private System.Data.DataTable ToDataTable(List<LogEntry> entries)
	{
		var table = new System.Data.DataTable();
		foreach (var column in Columns)
			table.Columns.Add(column, column switch
			{
				"Timestamp" => typeof(DateTime),
				"EventId"   => typeof(int),
				_           => typeof(string)
			});

		foreach (var e in entries)
			table.Rows.Add(
				e.Timestamp, e.Level.ToString(), e.EventId,
				(object?)e.EventName ?? DBNull.Value,
				e.Category, e.Message,
				(object?)e.MessageTemplate ?? DBNull.Value,
				PropertiesJson(e),
				(object?)e.Exception   ?? DBNull.Value,
				(object?)e.Path        ?? DBNull.Value,
				(object?)e.Method      ?? DBNull.Value,
				(object?)e.ClientIp    ?? DBNull.Value,
				(object?)e.Referer     ?? DBNull.Value,
				(object?)e.UserAgent   ?? DBNull.Value,
				(object?)e.SessionId   ?? DBNull.Value,
				(object?)e.SessionGuid ?? DBNull.Value);

		return table;
	}
}
