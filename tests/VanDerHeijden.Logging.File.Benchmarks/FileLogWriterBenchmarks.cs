using BenchmarkDotNet.Attributes;
using VanDerHeijden.Logging.File;

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class FileLogWriterBenchmarks
{
	private FileLogWriter writer = null!;
	private string logDirectory = null!;
	private List<string> batch = null!;

	// Varied batch sizes to measure the behaviour under different loads
	[Params(1, 10, 100, 500)]
	public int BatchSize { get; set; }

	// Varied message length: short (typical debug), medium (typical info), long (with stack trace)
	[Params(80, 256, 1024)]
	public int MessageLength { get; set; }

	// Number of times the batch is repeated per iteration so that the iteration time exceeds 100ms,
	// which BenchmarkDotNet needs for reliable measurements.
	// 2000 repetitions × ~60us per flush ≈ 120ms per iteration (smallest combination: BatchSize=1, 80 bytes).
	// For the largest combination (BatchSize=500, 1024 bytes) this becomes ~3s — acceptable with 3 iterations.
	private const int Repeat = 2000;

	[GlobalSetup]
	public void GlobalSetup()
	{
		logDirectory = Path.Combine(Path.GetTempPath(), $"bench-logs-{Guid.NewGuid():N}");
		Directory.CreateDirectory(logDirectory);
	}

	[IterationSetup]
	public void IterationSetup()
	{
		writer = new FileLogWriter(logDirectory);
		var message = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} [Information] " + new string('x', Math.Max(0, MessageLength - 40));
		batch = [.. Enumerable.Range(0, BatchSize).Select(i => $"{message} #{i}{Environment.NewLine}")];
	}

	[IterationCleanup]
	public void IterationCleanup() => writer.DisposeAsync().AsTask().GetAwaiter().GetResult();

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		try { Directory.Delete(logDirectory, recursive: true); } catch { }
	}

	/// <summary>
	/// Measures the mean time per WriteBatchAsync call over <see cref="Repeat"/> repetitions.
	/// One iteration writes Repeat × BatchSize messages so that the iteration time exceeds 100ms.
	/// </summary>
	[Benchmark(Description = "WriteBatchAsync", OperationsPerInvoke = Repeat)]
	[BenchmarkCategory("FileLogWriter")]
	public async Task WriteBatch()
	{
		for (var i = 0; i < Repeat; i++)
			await writer.WriteBatchAsync(batch, CancellationToken.None);
	}
}
