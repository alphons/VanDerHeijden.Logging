using BenchmarkDotNet.Attributes;
using System.Threading.Channels;
using VanDerHeijden.Logging;

/// <summary>
/// Meet de end-to-end throughput van BatchedLogger + FileLogWriter:
/// hoe snel kunnen berichten via de channel worden aangeboden (Write),
/// en hoe lang duurt het tot alles geflushed is (Dispose).
///
/// Logger en writer worden per iteratie opnieuw aangemaakt via IterationSetup/Cleanup
/// zodat elke meting met een frisse, niet-disposed instantie werkt.
/// </summary>
[MemoryDiagnoser]
public class BatchedLoggerBenchmarks
{
	private string logDirectory = null!;
	private string message = null!;
	private FileLogWriter fileWriter = null!;
	private BatchedLogger<string> logger = null!;

	[Params(1_000, 10_000, 100_000)]
	public int MessageCount { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		logDirectory = Path.Combine(Path.GetTempPath(), $"bench-batched-{Guid.NewGuid():N}");
		Directory.CreateDirectory(logDirectory);
		message = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [Information] Benchmark test message{Environment.NewLine}";
	}

	[IterationSetup]
	public void IterationSetup()
	{
		fileWriter = new FileLogWriter(logDirectory);
		logger = new BatchedLogger<string>(
			fileWriter,
			batchSize: 200,
			maxIdleMs: 500,
			fullMode: BoundedChannelFullMode.Wait);
	}

	[IterationCleanup]
	public void IterationCleanup()
	{
		// Dispose wacht tot de consumer alle berichten heeft geflushed naar schijf
		logger.Dispose();
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		try { Directory.Delete(logDirectory, recursive: true); } catch { }
	}

	/// <summary>
	/// Meet de volledige pipeline: N berichten enqueuen + wachten tot alles naar schijf is.
	/// De Dispose() in IterationCleanup wacht tot de consumer klaar is, maar valt buiten de meting.
	/// </summary>
	[Benchmark(Description = "End-to-end (enqueue + flush)")]
	public void EndToEnd()
	{
		for (var i = 0; i < MessageCount; i++)
			logger.Write(message);
	}
}
