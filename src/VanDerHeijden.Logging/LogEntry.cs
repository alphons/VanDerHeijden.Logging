using Microsoft.Extensions.Logging;

namespace VanDerHeijden.Logging;

/// <summary>
/// Represents a single structured log entry, shared by all writers.
/// </summary>
public class LogEntry
{
	/// <summary>Gets or sets the UTC timestamp when the log entry was created.</summary>
	public DateTime Timestamp { get; set; }

	/// <summary>Gets or sets the log level.</summary>
	public LogLevel Level { get; set; }

	/// <summary>Gets or sets the numeric event id (<c>0</c> when none was supplied).</summary>
	public int EventId { get; set; }

	/// <summary>Gets or sets the event name, or <see langword="null"/> if none was supplied.</summary>
	public string? EventName { get; set; }

	/// <summary>Gets or sets the logger category name.</summary>
	public string Category { get; set; } = string.Empty;

	/// <summary>Gets or sets the formatted log message.</summary>
	public string Message { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the original message template (e.g. <c>"User {UserId} logged in"</c>),
	/// or <see langword="null"/> when the log call carried no template.
	/// </summary>
	public string? MessageTemplate { get; set; }

	/// <summary>
	/// Gets or sets the structured properties extracted from the log state, or <see langword="null"/> when there are none.
	/// Values are snapshotted at log time and limited to <see cref="string"/>, <see cref="bool"/>, <see cref="int"/>,
	/// <see cref="long"/>, <see cref="double"/>, <see cref="decimal"/>, <see cref="Guid"/> and UTC <see cref="DateTime"/>;
	/// enums are stored by name. Objects and collections are stored as a <see cref="System.Text.Json.JsonElement"/>
	/// snapshot (JSON object or array); values that cannot be serialized are stored as their string representation.
	/// </summary>
	public Dictionary<string, object?>? Properties { get; set; }

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

	/// <summary>Gets or sets the session identifier, or <see langword="null"/> outside an HTTP context.</summary>
	public string? SessionId { get; set; }

	/// <summary>Gets or sets the application-level session GUID (from <c>HttpContext.Session["SessionGuid"]</c>), or <see langword="null"/> when not set.</summary>
	public string? SessionGuid { get; set; }
}
