using StackExchange.Redis;
using System.Text.Json;

namespace VanDerHeijden.Logging.Redis;

/// <summary>
/// Writes log entries to a Redis list using RPUSH.
/// Each entry is serialized as JSON. The list grows indefinitely unless
/// a consumer (e.g. Logstash, a worker service) drains it via LPOP/BLPOP.
/// Optionally a TTL can be set to auto-expire the key.
/// </summary>
public sealed class RedisLogWriter(
	IDatabase database,
	string listKey = "logs",
	TimeSpan? ttl = null) : IBatchedLogWriter<RedisLogEntry>
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	/// <summary>
	/// Serializes all entries as JSON and appends them to the Redis list in a single <c>RPUSH</c> command.
	/// If a TTL is configured, <c>EXPIREAT</c> is applied to the key after each write.
	/// </summary>
	/// <param name="entries">The log entries to push.</param>
	/// <param name="ct">A token that can cancel the operation.</param>
	public async Task WriteBatchAsync(List<RedisLogEntry> entries, CancellationToken ct)
	{
		var values = entries
			.Select(e => (RedisValue)JsonSerializer.Serialize(e, JsonOptions))
			.ToArray();

		await database.ListRightPushAsync(listKey, values);

		if (ttl.HasValue)
			await database.KeyExpireAsync(listKey, ttl.Value);
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
