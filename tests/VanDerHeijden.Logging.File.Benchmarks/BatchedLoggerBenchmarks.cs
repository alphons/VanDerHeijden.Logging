using BenchmarkDotNet.Attributes;
using System.Threading.Channels;
using VanDerHeijden.Logging;

/// <summary>
/// Meet de end-to-end throughput van BatchedLogger + FileLogWriter:
/// hoe snel kunnen berichten via de channel worden aangeboden (Write),
/// en hoe lang duurt het tot alles geflushed is (Dispose).
/// </summary>
[MemoryDiagnoser]
public class BatchedLoggerBenchmarks
{
	private string logDirectory = null!;

	[Params(1_000, 10_000, 100_000)]
	public int MessageCount { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		logDirectory = Path.Combine(Path.GetTempPath(), $"bench-batched-{Guid.NewGuid():N}");
		Directory.CreateDirectory(logDirectory);
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		try { Directory.Delete(logDirectory, recursive: true); } catch { }
	}

	/// <summary>
	/// Schrijft N berichten via BatchedLogger en wacht tot alles geflushed is (Dispose).
	/// Dit meet de volledige pipeline: channel enqueue → batch flush → disk write.
	/// </summary>
	[Benchmark(Description = "BatchedLogger end-to-end")]
	public void EndToEnd()
	{
		var fileWriter = new FileLogWriter(logDirectory);
		using var logger = new BatchedLogger<string>(
			fileWriter,
			batchSize: 200,
			maxIdleMs: 500,
			fullMode: BoundedChannelFullMode.Wait);

		var message = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [Information] Benchmark test message{Environment.NewLine}";
		for (var i = 0; i < MessageCount; i++)
			logger.Write(message);

		// Dispose wacht tot de consumer alle berichten heeft verwerkt
		logger.Dispose();
	}

	/// <summary>
	/// Meet de Write()-throughput los van disk I/O:
	/// hoe snel de channel gevuld kan worden zonder te wachten op flush.
	/// Laat zien of de bottleneck in de channel of in de disk zit.
	/// </summary>
	[Benchmark(Description = "Write throughput (channel only)")]
	public async Task WriteThroughput()
	{
		var fileWriter = new FileLogWriter(logDirectory);
		using var logger = new BatchedLogger<string>(
			fileWriter,
			batchSize: 200,
			maxIdleMs: 500,
			fullMode: BoundedChannelFullMode.Wait);

		var message = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [Information] Benchmark test message{Environment.NewLine}";

		var sw = System.Diagnostics.Stopwatch.StartNew();
		for (var i = 0; i < MessageCount; i++)
			logger.Write(message);
		sw.Stop();

		// Geef consumer ruim de tijd om te flushen zodat de file writer netjes sluit
		await Task.Delay(2000);
		logger.Dispose();
	}
}
