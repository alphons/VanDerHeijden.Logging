namespace VanDerHeijden.Logging;

public sealed class FileLogWriter(string logDirectory = "Logs") : IBatchedLogWriter<string>
{
	private StreamWriter? writer;
	private DateTime currentDate = DateTime.MinValue;

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

	public async ValueTask DisposeAsync()
	{
		if (writer == null) return;
		try { await writer.FlushAsync(); await writer.DisposeAsync(); }
		catch { }
		finally { writer = null; }
	}
}