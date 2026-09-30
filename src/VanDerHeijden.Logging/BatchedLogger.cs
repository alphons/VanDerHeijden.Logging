using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Globalization;
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
	private readonly BoundedChannelFullMode fullMode;
	private volatile bool disposed;

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
		this.fullMode = fullMode;

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
	/// With the drop modes the call never blocks. Entries written after <see cref="Dispose"/> are discarded.
	/// </summary>
	/// <param name="entry">The entry to enqueue.</param>
	public void Write(T entry)
	{
		if (channel.Writer.TryWrite(entry) || fullMode != BoundedChannelFullMode.Wait) return;

		// Channel is full: block until there is room. Polling (instead of blocking on WaitToWriteAsync)
		// keeps the wake-up independent of the thread pool, which the blocked callers may be exhausting.
		var spinner = new SpinWait();
		while (!disposed && !channel.Writer.TryWrite(entry))
			spinner.SpinOnce();
	}

	private async Task ConsumeAsync(CancellationToken ct)
	{
		var batch = new List<T>(batchSize);
		var reader = channel.Reader;

		try
		{
			while (true)
			{
				// Drain everything that is already queued; this path allocates nothing per entry.
				while (batch.Count < batchSize && reader.TryRead(out var entry))
					batch.Add(entry);

				if (batch.Count >= batchSize)
				{
					await ExecuteWriteAsync(batch, ct);
					batch.Clear();
					continue;
				}

				if (batch.Count == 0)
				{
					// Nothing pending: wait until an entry arrives or the channel is completed.
					if (!await reader.WaitToReadAsync(ct)) break;
					continue;
				}

				// Partial batch: wait at most maxIdleMs for more entries, then flush what we have.
				using var idleCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
				idleCts.CancelAfter(maxIdleMs);
				try
				{
					if (!await reader.WaitToReadAsync(idleCts.Token)) break;
				}
				catch (OperationCanceledException) when (!ct.IsCancellationRequested)
				{
					await ExecuteWriteAsync(batch, ct);
					batch.Clear();
				}
			}
		}
		catch { }

		// Channel completed or shutdown requested: flush what is left.
		if (batch.Count > 0)
		{
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
	/// entries, then disposes the writer and other resources.
	/// </summary>
	public void Dispose()
	{
		disposed = true;
		channel.Writer.TryComplete();
		cts.CancelAfter(TimeSpan.FromSeconds(8));
		try { consumerTask.Wait(TimeSpan.FromSeconds(10)); } catch { }
		try { writer.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2)); } catch { }
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
/// A factory that converts the captured <see cref="LogEntry"/> into a <typeparamref name="T"/> entry.
/// It runs synchronously on the logging thread. Writers that store <see cref="LogEntry"/> directly pass <c>e =&gt; e</c>.
/// </param>
/// <param name="httpContextAccessor">
/// Optional <see cref="IHttpContextAccessor"/> used to enrich log entries with request metadata.
/// When <see langword="null"/>, HTTP properties are omitted.
/// </param>
public sealed class BatchedLoggerProvider<T>(
	BatchedLogger<T> batchedLogger,
	Func<LogEntry, T> entryFactory,
	IHttpContextAccessor? httpContextAccessor = null) : ILoggerProvider
{
	/// <summary>
	/// Creates an <see cref="ILogger"/> for the given category name.
	/// </summary>
	/// <param name="categoryName">The category name for messages produced by the logger.</param>
	/// <returns>An <see cref="ILogger"/> instance.</returns>
	public ILogger CreateLogger(string categoryName) =>
		new BatchedCategoryLogger<T>(batchedLogger, categoryName, entryFactory, httpContextAccessor);

	/// <summary>
	/// Disposes the underlying <see cref="BatchedLogger{T}"/>, flushing any remaining entries.
	/// </summary>
	public void Dispose() => batchedLogger.Dispose();
}

internal sealed class BatchedCategoryLogger<T>(
	BatchedLogger<T> batchedLogger,
	string categoryName,
	Func<LogEntry, T> entryFactory,
	IHttpContextAccessor? httpContextAccessor) : ILogger
{
	private const string OriginalFormatKey = "{OriginalFormat}";

	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
	public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		if (!IsEnabled(logLevel)) return;

		var entry = new LogEntry
		{
			Timestamp = DateTime.UtcNow,
			Level     = logLevel,
			EventId   = eventId.Id,
			EventName = eventId.Name,
			Category  = categoryName,
			Message   = formatter(state, exception),
			Exception = exception?.ToString()
		};

		if (state is IReadOnlyList<KeyValuePair<string, object?>> properties)
			ExtractProperties(entry, properties);

		ApplyHttpContext(entry);
		batchedLogger.Write(entryFactory(entry));
	}

	private static void ExtractProperties(LogEntry entry, IReadOnlyList<KeyValuePair<string, object?>> properties)
	{
		for (int i = 0; i < properties.Count; i++)
		{
			var (key, value) = properties[i];
			if (key == OriginalFormatKey)
			{
				entry.MessageTemplate = value as string;
				continue;
			}
			(entry.Properties ??= new(properties.Count))[key] = Normalize(value);
		}
	}

	// Snapshot values on the logging thread: batches are written later, and mutable objects may have changed by then.
	// The result is limited to a small set of immutable types every writer can serialize.
	private static object? Normalize(object? value) => value switch
	{
		null or string or bool or int or long or double or decimal or Guid => value,
		DateTime dt => dt.ToUniversalTime(),
		DateTimeOffset dto => dto.UtcDateTime,
		float f => (double)f,
		sbyte or byte or short or ushort or uint => Convert.ToInt64(value),
		Enum e => e.ToString(),
		IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
		_ => LogValueSnapshot.Capture(value)
	};

	private void ApplyHttpContext(LogEntry entry)
	{
		if (httpContextAccessor?.HttpContext is not { } ctx) return;

		string? ip = ctx.Connection.RemoteIpAddress?.ToString();
		string? forwarded = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
		if (!string.IsNullOrEmpty(forwarded))
			ip = forwarded.Split(',')[0].Trim();

		try
		{
			var session = ctx.Session;
			entry.SessionId = session.Id;
			if (session.TryGetValue("SessionGuid", out var bytes))
				entry.SessionGuid = System.Text.Encoding.UTF8.GetString(bytes);
		}
		catch (InvalidOperationException) { }

		entry.Path      = ctx.Request.Path.ToString();
		entry.Method    = ctx.Request.Method;
		entry.ClientIp  = ip ?? "Unknown";
		entry.Referer   = ctx.Request.Headers["Referer"].ToString();
		entry.UserAgent = ctx.Request.Headers["User-Agent"].ToString();
	}
}
