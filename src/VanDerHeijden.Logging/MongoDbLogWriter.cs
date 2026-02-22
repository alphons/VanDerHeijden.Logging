

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace LoggerExtensions;

public class LogEntry
{
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
	public DateTime Timestamp { get; set; }
	public string Level { get; set; } = string.Empty;
	public string Category { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
	public string? Exception { get; set; }
}

public sealed class MongoDbLogWriter(IMongoCollection<LogEntry> collection) : IBatchedLogWriter<LogEntry>, IAsyncDisposable
{
	public async Task WriteBatchAsync(List<LogEntry> entries, CancellationToken ct) =>
		await collection.InsertManyAsync(entries, cancellationToken: ct);

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}