using BenchmarkDotNet.Attributes;
using System.Threading.Channels;
using VanDerHeijden.Logging;
using VanDerHeijden.Logging.File;

/// <summary>
/// Measures the end-to-end throughput of BatchedLogger + FileLogWriter:
/// how fast messages can be offered to the channel (Write),
/// and how long it takes until everything has been flushed (Dispose).
///
/// The logger and writer are recreated for every iteration via IterationSetup/Cleanup
/// so that each measurement uses a fresh, non-disposed instance.
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
		message = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} [Information] Benchmark test message{Environment.NewLine}";
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
		// Dispose waits until the consumer has flushed all messages to disk
		logger.Dispose();
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		try { Directory.Delete(logDirectory, recursive: true); } catch { }
	}

	/// <summary>
	/// Measures the full pipeline: enqueueing N messages + waiting until everything is on disk.
	/// The Dispose() in IterationCleanup waits for the consumer to finish, but falls outside the measurement.
	/// </summary>
	[Benchmark(Description = "End-to-end (enqueue + flush)")]
	public void EndToEnd()
	{
		for (var i = 0; i < MessageCount; i++)
			logger.Write(message);
	}
}
