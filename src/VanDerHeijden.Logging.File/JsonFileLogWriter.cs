namespace VanDerHeijden.Logging.File;

/// <summary>
/// Writes batches of <see cref="LogEntry"/> as JSON Lines (one JSON object per line) to a daily rotating file.
/// A new file is opened automatically whenever the UTC calendar date changes.
/// </summary>
/// <param name="logDirectory">
/// Directory in which log files are created. Defaults to <c>"Logs"</c>.
/// Files follow the naming pattern <c>log-yyyyMMdd.jsonl</c>.
/// </param>
public sealed class JsonFileLogWriter(string logDirectory = "Logs") : IBatchedLogWriter<LogEntry>
{
	private readonly LogEntryJsonWriter json = new();
	private FileStream? stream;
	private DateTime currentDate = DateTime.MinValue;

	/// <summary>
	/// Appends all entries in the batch to the current day's log file, one JSON object per line.
	/// If the date has changed since the last write, the previous file is closed
	/// and a new one is opened.
	/// </summary>
	/// <param name="entries">The log entries to write.</param>
	/// <param name="ct">A token that can cancel the operation.</param>
	public async Task WriteBatchAsync(List<LogEntry> entries, CancellationToken ct)
	{
		var today = DateTime.UtcNow.Date;
		if (stream == null || today != currentDate)
		{
			await DisposeAsync();
			currentDate = today;
			Directory.CreateDirectory(logDirectory);
			stream = new FileStream(
				Path.Combine(logDirectory, $"log-{today:yyyyMMdd}.jsonl"),
				FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 65536, useAsync: true);
		}

		json.Clear();
		foreach (var entry in entries)
			json.WriteLine(entry);

		await stream.WriteAsync(json.WrittenMemory, ct);
		await stream.FlushAsync(ct);
	}

	/// <summary>
	/// Flushes and closes the current log file, releasing all file handles.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		if (stream == null) return;
		try { await stream.FlushAsync(); await stream.DisposeAsync(); }
		catch { }
		finally { stream = null; }
	}
}
