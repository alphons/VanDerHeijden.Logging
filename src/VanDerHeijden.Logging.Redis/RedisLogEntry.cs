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

	/// <summary>Gets or sets the request path (e.g. <c>"/api/users"</c>), or <see langword="null"/> outside an HTTP context.</summary>
	public string? Path { get; set; }

	/// <summary>Gets or sets the HTTP method (e.g. <c>"GET"</c>), or <see langword="null"/> outside an HTTP context.</summary>
	public string? Method { get; set; }

	/// <summary>Gets or sets the client IP address, or <see langword="null"/> outside an HTTP context.</summary>
	public string? ClientIp { get; set; }

	/// <summary>Gets or sets the Referer header value, or <see langword="null"/> outside an HTTP context.</summary>
	public string? Referer { get; set; }

	/// <summary>Gets or sets the User-Agent header value, or <see langword="null"/> outside an HTTP context.</summary>
	public string? UserAgent { get; set; }
}
