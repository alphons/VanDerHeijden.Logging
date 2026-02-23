namespace VanDerHeijden.Logging.Redis;

/// <summary>
/// Represents a single log entry serialized as JSON and pushed to a Redis list.
/// </summary>
public class RedisLogEntry
{
	/// <summary>Gets or sets the UTC timestamp when the log entry was created.</summary>
	public DateTime Timestamp { get; set; }

	/// <summary>Gets or sets the log level (e.g. <c>"Information"</c>, <c>"Error"</c>).</summary>
	public string Level { get; set; } = string.Empty;

	/// <summary>Gets or sets the logger category name.</summary>
	public string Category { get; set; } = string.Empty;

	/// <summary>Gets or sets the formatted log message.</summary>
	public string Message { get; set; } = string.Empty;

	/// <summary>Gets or sets the string representation of an associated exception, or <see langword="null"/> if none.</summary>
	public string? Exception { get; set; }
}
