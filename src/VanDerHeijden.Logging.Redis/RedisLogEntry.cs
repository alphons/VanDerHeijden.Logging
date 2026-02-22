namespace VanDerHeijden.Logging.Redis;

public class RedisLogEntry
{
	public DateTime Timestamp { get; set; }
	public string Level { get; set; } = string.Empty;
	public string Category { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
	public string? Exception { get; set; }
}
