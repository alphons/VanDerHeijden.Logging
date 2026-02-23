using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace VanDerHeijden.Logging;

/// <summary>
/// Defines a writer that receives a batch of log entries and persists them to a backing store.
/// </summary>
/// <typeparam name="T">The type of log entry.</typeparam>
public interface IBatchedLogWriter<T> : IAsyncDisposable
{
	/// <summary>
	/// Writes a batch of log entries to the backing store.
	/// </summary>
	/// <param name="entries">The entries to write.</param>
	/// <param name="ct">A token that can cancel the operation.</param>
	Task WriteBatchAsync(List<T> entries, CancellationToken ct);
}

/// <summary>
/// Buffers log entries in a bounded channel and flushes them in batches via an <see cref="IBatchedLogWriter{T}"/>.
/// Entries are flushed when the batch reaches the configured batch size or after
/// the configured idle timeout, whichever comes first.
/// </summary>
/// <typeparam name="T">The type of log entry.</typeparam>
public sealed class BatchedLogger<T> : IDisposable
{
	private readonly Channel<T> channel;
	private readonly Task consumerTask;
	private readonly CancellationTokenSource cts = new();
	private readonly IBatchedLogWriter<T> writer;
	private readonly int batchSize;
	private readonly int maxIdleMs;

	/// <summary>
	/// Initializes a new <see cref="BatchedLogger{T}"/>.
	/// </summary>
	/// <param name="writer">The writer that persists batches.</param>
	/// <param name="batchSize">Maximum number of entries per batch before an immediate flush is triggered.</param>
	/// <param name="maxIdleMs">Maximum time in milliseconds to wait before flushing a non-full batch.</param>
	/// <param name="fullMode">Behaviour when the internal channel is full.</param>
	public BatchedLogger(
		IBatchedLogWriter<T> writer,
		int batchSize = 200,
		int maxIdleMs = 4000,
		BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait)
	{
		this.writer = writer;
		this.batchSize = batchSize;
		this.maxIdleMs = maxIdleMs;

		channel = Channel.CreateBounded<T>(new BoundedChannelOptions(10000)
		{
			SingleReader = true,
			SingleWriter = false,
			FullMode = fullMode
		});

		consumerTask = Task.Run(() => ConsumeAsync(cts.Token));
	}

	/// <summary>
	/// Enqueues a log entry. If the channel is full and the <c>fullMode</c> is
	/// <see cref="BoundedChannelFullMode.Wait"/>, the call blocks until space is available.
	/// </summary>
	/// <param name="entry">The entry to enqueue.</param>
	public void Write(T entry) => channel.Writer.TryWrite(entry);

	private async Task ConsumeAsync(CancellationToken ct)
	{
		var batch = new List<T>(batchSize);
		Task<T>? readTask = null;

		try
		{
			while (!ct.IsCancellationRequested)
			{
				readTask ??= channel.Reader.ReadAsync(ct).AsTask();

				using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
				var delayTask = Task.Delay(maxIdleMs, delayCts.Token);
				var completed = await Task.WhenAny(readTask, delayTask);

				if (completed == delayTask)
				{
					if (batch.Count > 0)
					{
						await ExecuteWriteAsync(batch, ct);
						batch.Clear();
					}
					continue;
				}

				delayCts.Cancel();
				batch.Add(await readTask);
				readTask = null;

				if (batch.Count >= batchSize)
				{
					await ExecuteWriteAsync(batch, ct);
					batch.Clear();
				}
			}
		}
		catch (OperationCanceledException)
		{
			if (batch.Count > 0)
				try { await ExecuteWriteAsync(batch, CancellationToken.None); } catch { }
		}
	}

	private async Task ExecuteWriteAsync(List<T> batch, CancellationToken ct)
	{
		const int maxRetries = 3;

		for (int attempt = 0; attempt < maxRetries; attempt++)
		{
			try
			{
				await writer.WriteBatchAsync(batch, ct);
				return;
			}
			catch (Exception) when (attempt < maxRetries - 1)
			{
				await Task.Delay(100 << attempt, ct);
			}
			catch { return; } // swallow on final attempt — logging must never crash the app
		}
	}

	/// <summary>
	/// Signals the channel as complete, waits up to 10 seconds for the consumer to flush remaining
	/// entries, then disposes resources.
	/// </summary>
	public void Dispose()
	{
		channel.Writer.Complete();
		cts.CancelAfter(TimeSpan.FromSeconds(8));
		try { consumerTask.Wait(TimeSpan.FromSeconds(10)); } catch { }
		cts.Dispose();
	}
}

/// <summary>
/// An <see cref="ILoggerProvider"/> that creates <see cref="ILogger"/> instances backed by a
/// shared <see cref="BatchedLogger{T}"/>.
/// </summary>
/// <typeparam name="T">The type of log entry produced by <paramref name="entryFactory"/>.</typeparam>
/// <param name="batchedLogger">The shared batched logger used by all created loggers.</param>
/// <param name="entryFactory">
/// A factory that converts a formatted message string and <see cref="LogLevel"/> into a <typeparamref name="T"/> entry.
/// </param>
public sealed class BatchedLoggerProvider<T>(BatchedLogger<T> batchedLogger, Func<string, LogLevel, T> entryFactory) : ILoggerProvider
{
	/// <summary>
	/// Creates an <see cref="ILogger"/> for the given category name.
	/// </summary>
	/// <param name="categoryName">The category name for messages produced by the logger.</param>
	/// <returns>An <see cref="ILogger"/> instance.</returns>
	public ILogger CreateLogger(string categoryName) =>
		new BatchedCategoryLogger<T>(batchedLogger, categoryName, entryFactory);

	/// <summary>
	/// Disposes the underlying <see cref="BatchedLogger{T}"/>, flushing any remaining entries.
	/// </summary>
	public void Dispose() => batchedLogger.Dispose();
}

internal sealed class BatchedCategoryLogger<T>(BatchedLogger<T> batchedLogger, string categoryName, Func<string, LogLevel, T> entryFactory) : ILogger
{
	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
	public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		if (!IsEnabled(logLevel)) return;
		batchedLogger.Write(entryFactory($"{categoryName}: {formatter(state, exception)}{(exception != null ? $"{Environment.NewLine}{exception}" : "")}", logLevel));
	}
}
