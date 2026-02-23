using Microsoft.Data.SqlClient;

namespace VanDerHeijden.Logging.Sql;

/// <summary>
/// Writes log entries to a SQL Server table in bulk using SqlBulkCopy.
/// Expected table schema:
///   CREATE TABLE Logs (
///     Id        BIGINT IDENTITY PRIMARY KEY,
///     Timestamp DATETIME2       NOT NULL,
///     Level     NVARCHAR(20)    NOT NULL,
///     Category  NVARCHAR(256)   NOT NULL,
///     Message   NVARCHAR(MAX)   NOT NULL,
///     Exception NVARCHAR(MAX)   NULL
///   );
/// </summary>
public sealed class SqlLogWriter(string connectionString, string tableName = "Logs") : IBatchedLogWriter<SqlLogEntry>
{
	/// <summary>
	/// Bulk-inserts all entries into the configured SQL Server table using <see cref="SqlBulkCopy"/>.
	/// </summary>
	/// <param name="entries">The log entries to insert.</param>
	/// <param name="ct">A token that can cancel the operation.</param>
	public async Task WriteBatchAsync(List<SqlLogEntry> entries, CancellationToken ct)
	{
		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync(ct);

		using var bulkCopy = new SqlBulkCopy(connection)
		{
			DestinationTableName = tableName,
			BulkCopyTimeout = 30
		};

		bulkCopy.ColumnMappings.Add(nameof(SqlLogEntry.Timestamp), "Timestamp");
		bulkCopy.ColumnMappings.Add(nameof(SqlLogEntry.Level),     "Level");
		bulkCopy.ColumnMappings.Add(nameof(SqlLogEntry.Category),  "Category");
		bulkCopy.ColumnMappings.Add(nameof(SqlLogEntry.Message),   "Message");
		bulkCopy.ColumnMappings.Add(nameof(SqlLogEntry.Exception), "Exception");

		var table = ToDataTable(entries);
		await bulkCopy.WriteToServerAsync(table, ct);
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	private static System.Data.DataTable ToDataTable(List<SqlLogEntry> entries)
	{
		var table = new System.Data.DataTable();
		table.Columns.Add("Timestamp", typeof(DateTime));
		table.Columns.Add("Level",     typeof(string));
		table.Columns.Add("Category",  typeof(string));
		table.Columns.Add("Message",   typeof(string));
		table.Columns.Add("Exception", typeof(string));

		foreach (var e in entries)
			table.Rows.Add(e.Timestamp, e.Level, e.Category, e.Message, (object?)e.Exception ?? DBNull.Value);

		return table;
	}
}
