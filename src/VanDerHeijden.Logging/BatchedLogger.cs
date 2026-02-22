using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace VanDerHeijden.Logging;

public interface IBatchedLogWriter<T> : IAsyncDisposable
{
	Task WriteBatchAsync(List<T> entries, CancellationToken ct);
}

public sealed class BatchedLogger<T> : IDisposable
{
	private readonly Channel<T> channel;
	private readonly Task consumerTask;
	private readonly CancellationTokenSource cts = new();
	private readonly IBatchedLogWriter<T> writer;
	private readonly int batchSize;
	private readonly int maxIdleMs;

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

	public void Dispose()
	{
		channel.Writer.Complete();
		cts.CancelAfter(TimeSpan.FromSeconds(8));
		try { consumerTask.Wait(TimeSpan.FromSeconds(10)); } catch { }
		cts.Dispose();
	}
}

// Generic provider + category logger, reusable for any entry type T
public sealed class BatchedLoggerProvider<T>(BatchedLogger<T> batchedLogger, Func<string, LogLevel, T> entryFactory) : ILoggerProvider
{
	public ILogger CreateLogger(string categoryName) =>
		new BatchedCategoryLogger<T>(batchedLogger, categoryName, entryFactory);

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