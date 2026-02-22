using BenchmarkDotNet.Attributes;
using VanDerHeijden.Logging.File;

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class FileLogWriterBenchmarks
{
	private FileLogWriter writer = null!;
	private string logDirectory = null!;
	private List<string> batch = null!;

	// Gevarieerde batch groottes om het gedrag onder verschillende loads te meten
	[Params(1, 10, 100, 500)]
	public int BatchSize { get; set; }

	// Gevarieerde berichtlengte: kort (typisch debug), middel (typisch info), lang (met stack trace)
	[Params(80, 256, 1024)]
	public int MessageLength { get; set; }

	// Aantal keer dat de batch per iteratie wordt herhaald zodat de iteratietijd >100ms wordt,
	// wat BenchmarkDotNet nodig heeft voor betrouwbare metingen.
	// 2000 herhalingen × ~60us per flush ≈ 120ms per iteratie (kleinste combinatie: BatchSize=1, 80 bytes).
	// Voor de grootste combinatie (BatchSize=500, 1024 bytes) wordt dit ~3s — acceptabel bij 3 iteraties.
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
		var message = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [Information] " + new string('x', Math.Max(0, MessageLength - 40));
		batch = Enumerable.Range(0, BatchSize)
			.Select(i => $"{message} #{i}{Environment.NewLine}")
			.ToList();
	}

	[IterationCleanup]
	public void IterationCleanup() => writer.DisposeAsync().AsTask().GetAwaiter().GetResult();

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		try { Directory.Delete(logDirectory, recursive: true); } catch { }
	}

	/// <summary>
	/// Meet de gemiddelde tijd per WriteBatchAsync-aanroep over <see cref="Repeat"/> herhalingen.
	/// Eén iteratie schrijft Repeat × BatchSize berichten zodat de iteratietijd >100ms is.
	/// </summary>
	[Benchmark(Description = "WriteBatchAsync", OperationsPerInvoke = Repeat)]
	[BenchmarkCategory("FileLogWriter")]
	public async Task WriteBatch()
	{
		for (var i = 0; i < Repeat; i++)
			await writer.WriteBatchAsync(batch, CancellationToken.None);
	}
}
