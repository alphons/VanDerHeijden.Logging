using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using VanDerHeijden.Logging;

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class FileLogWriterBenchmarks : IAsyncDisposable
{
	private FileLogWriter writer = null!;
	private string logDirectory = null!;

	// Gevarieerde batch groottes om het gedrag onder verschillende loads te meten
	[Params(1, 10, 100, 500)]
	public int BatchSize { get; set; }

	// Gevarieerde berichtlengte: kort (typisch debug), middel (typisch info), lang (met stack trace)
	[Params(80, 256, 1024)]
	public int MessageLength { get; set; }

	private List<string> batch = null!;

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
	public async Task IterationCleanup() => await writer.DisposeAsync();

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		try { Directory.Delete(logDirectory, recursive: true); } catch { }
	}

	/// <summary>
	/// Meet de ruwe WriteBatchAsync doorvoer: hoeveel tijd kost 1 batch flush naar schijf.
	/// </summary>
	[Benchmark(Description = "WriteBatchAsync")]
	public async Task WriteBatch()
	{
		await writer.WriteBatchAsync(batch, CancellationToken.None);
	}

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
