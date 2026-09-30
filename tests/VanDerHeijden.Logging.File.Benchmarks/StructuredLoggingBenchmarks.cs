using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using System.Threading.Channels;
using VanDerHeijden.Logging;
using VanDerHeijden.Logging.File;
using VanDerHeijden.Logging.MongoDb;

public enum BenchOrderStatus { Pending, Shipped }

public sealed record BenchAddress(string Street, string City, string Country);

public sealed record BenchCustomer(string Name, int Number, string[] Tags, BenchAddress Address, BenchOrderStatus Status);

/// <summary>
/// Discards every batch, so only the cost of the logging call itself is measured.
/// </summary>
public sealed class NullLogWriter<T> : IBatchedLogWriter<T>
{
	public Task WriteBatchAsync(List<T> entries, CancellationToken ct) => Task.CompletedTask;
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Measures the cost of one ILogger call on the calling thread: building the LogEntry, extracting the
/// properties and snapshotting objects as JSON. The writer discards everything and the channel never blocks.
/// The "Writer thread" benchmarks measure the work done later, on the background thread, per entry.
/// </summary>
[MemoryDiagnoser]
public class StructuredLoggingBenchmarks
{
	private static readonly Guid OrderId = Guid.NewGuid();
	private static readonly BenchCustomer Customer = new(
		"Alice", 7, ["vip", "nl", "newsletter"], new BenchAddress("Main Street 1", "Zwolle", "NL"), BenchOrderStatus.Shipped);

	private BatchedLoggerProvider<LogEntry> entryProvider = null!;
	private ILogger entryLogger = null!;
	private LogEntryJsonWriter jsonWriter = null!;
	private LogEntry objectEntry = null!;

	[GlobalSetup]
	public void GlobalSetup()
	{
		entryProvider = new BatchedLoggerProvider<LogEntry>(
			new BatchedLogger<LogEntry>(new NullLogWriter<LogEntry>(), fullMode: BoundedChannelFullMode.DropOldest),
			entry => { objectEntry = entry; return entry; });

		entryLogger = entryProvider.CreateLogger("Benchmark");
		jsonWriter = new LogEntryJsonWriter();

		// Capture one entry with an object property for the serialization benchmark.
		entryLogger.LogInformation("Order {OrderId} for {Customer}", OrderId, Customer);
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		entryProvider.Dispose();
	}

	[Benchmark(Description = "Log call: plain message")]
	public void EntryPlain() => entryLogger.LogInformation("Fast logging");

	[Benchmark(Description = "Log call: 3 primitives")]
	public void EntryPrimitives() => entryLogger.LogInformation("Order {OrderId} count {Count} amount {Amount}", OrderId, 42, 12.34m);

	[Benchmark(Description = "Log call: object")]
	public void EntryObject() => entryLogger.LogInformation("Order {OrderId} for {Customer}", OrderId, Customer);


	[Benchmark(Description = "Writer thread: JSON line for object entry")]
	public int JsonLine()
	{
		jsonWriter.Clear();
		jsonWriter.WriteLine(objectEntry);
		return jsonWriter.WrittenMemory.Length;
	}

	[Benchmark(Description = "Writer thread: BSON document for object entry")]
	public BsonDocument BsonDocumentForEntry() => LogEntryBsonConverter.ToDocument(objectEntry);
}

/// <summary>
/// End-to-end: 100,000 log calls that each carry an object, written as JSON Lines to disk.
/// The channel uses Wait mode, so the measurement includes the backpressure of the file writer.
/// </summary>
[MemoryDiagnoser]
public class StructuredFileBenchmarks
{
	private static readonly Guid OrderId = Guid.NewGuid();
	private static readonly BenchCustomer Customer = new(
		"Alice", 7, ["vip", "nl", "newsletter"], new BenchAddress("Main Street 1", "Zwolle", "NL"), BenchOrderStatus.Shipped);

	private string logDirectory = null!;
	private BatchedLoggerProvider<LogEntry> provider = null!;
	private ILogger logger = null!;

	[Params(100_000)]
	public int MessageCount { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		logDirectory = Path.Combine(Path.GetTempPath(), $"bench-jsonl-{Guid.NewGuid():N}");
		Directory.CreateDirectory(logDirectory);
	}

	[IterationSetup]
	public void IterationSetup()
	{
		provider = new BatchedLoggerProvider<LogEntry>(
			new BatchedLogger<LogEntry>(new JsonFileLogWriter(logDirectory), batchSize: 200, maxIdleMs: 500, fullMode: BoundedChannelFullMode.Wait),
			entry => entry);
		logger = provider.CreateLogger("Benchmark");
	}

	[IterationCleanup]
	public void IterationCleanup() => provider.Dispose();

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		try { Directory.Delete(logDirectory, recursive: true); } catch { }
	}

	[Benchmark(Description = "100k object entries to .jsonl")]
	public void LogObjects()
	{
		for (var i = 0; i < MessageCount; i++)
			logger.LogInformation("Order {OrderId} number {Number} for {Customer}", OrderId, i, Customer);
	}
}
