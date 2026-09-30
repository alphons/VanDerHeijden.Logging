using System.Threading.Channels;

namespace VanDerHeijden.Logging;

/// <summary>
/// Batching settings shared by all loggers. Each logger starts from its own defaults
/// (see the <c>Add…Logger</c> method) and lets you override them.
/// </summary>
public class BatchedLoggerOptions
{
	/// <summary>
	/// Gets or sets the maximum number of entries written per batch. A full batch is written immediately.
	/// </summary>
	public int BatchSize { get; set; } = 200;

	/// <summary>
	/// Gets or sets the maximum time in milliseconds a non-full batch waits for more entries before it is written.
	/// </summary>
	public int MaxIdleMs { get; set; } = 4000;

	/// <summary>
	/// Gets or sets the number of entries the in-memory queue can hold while the writer is busy.
	/// </summary>
	public int QueueCapacity { get; set; } = 10000;

	/// <summary>
	/// Gets or sets what happens when the queue is full: <see cref="BoundedChannelFullMode.Wait"/> blocks the
	/// logging call until there is room (nothing is lost), the drop modes never block and discard entries instead.
	/// </summary>
	public BoundedChannelFullMode FullMode { get; set; } = BoundedChannelFullMode.Wait;
}
