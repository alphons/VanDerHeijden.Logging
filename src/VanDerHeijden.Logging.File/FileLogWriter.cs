namespace VanDerHeijden.Logging.File;

/// <summary>
/// Writes batches of log messages to a daily rotating text file.
/// A new file is opened automatically whenever the calendar date changes.
/// </summary>
/// <param name="logDirectory">
/// Directory in which log files are created. Defaults to <c>"Logs"</c>.
/// Files follow the naming pattern <c>log-yyyyMMdd.txt</c>.
/// </param>
public sealed class FileLogWriter(string logDirectory = "Logs") : IBatchedLogWriter<string>
{
	private StreamWriter? writer;
	private DateTime currentDate = DateTime.MinValue;

	/// <summary>
	/// Appends all messages in the batch to the current day's log file.
	/// If the date has changed since the last write, the previous file is closed
	/// and a new one is opened.
	/// </summary>
	/// <param name="messages">The pre-formatted log lines to write.</param>
	/// <param name="ct">A token that can cancel the operation.</param>
	public async Task WriteBatchAsync(List<string> messages, CancellationToken ct)
	{
		var today = DateTime.Today;
		if (writer == null || today != currentDate)
		{
			await DisposeAsync();
			currentDate = today;
			Directory.CreateDirectory(logDirectory);
			var stream = new FileStream(
				Path.Combine(logDirectory, $"log-{today:yyyyMMdd}.txt"),
				FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 65536, useAsync: true);
			writer = new StreamWriter(stream) { AutoFlush = false };
		}

		foreach (var msg in messages)
			await writer.WriteAsync(msg.AsMemory(), ct);

		await writer.FlushAsync(ct);
	}

	/// <summary>
	/// Flushes and closes the current log file, releasing all file handles.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		if (writer == null) return;
		try { await writer.FlushAsync(); await writer.DisposeAsync(); }
		catch { }
		finally { writer = null; }
	}
}
