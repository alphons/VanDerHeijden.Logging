using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace VanDerHeijden.Logging.MongoDb;

/// <summary>
/// Represents a single log entry stored in MongoDB.
/// </summary>
public class LogEntry
{
	/// <summary>Gets or sets the MongoDB document identifier.</summary>
	[BsonId]
	[BsonRepresentation(BsonType.ObjectId)]
	public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

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

/// <summary>
/// Writes batches of <see cref="LogEntry"/> documents to a MongoDB collection using <c>InsertManyAsync</c>.
/// </summary>
/// <param name="collection">The MongoDB collection that receives log entries.</param>
public sealed class MongoDbLogWriter(IMongoCollection<LogEntry> collection) : IBatchedLogWriter<LogEntry>
{
	/// <summary>
	/// Inserts all entries in the batch into the MongoDB collection.
	/// </summary>
	/// <param name="entries">The log entries to insert.</param>
	/// <param name="ct">A token that can cancel the operation.</param>
	public async Task WriteBatchAsync(List<LogEntry> entries, CancellationToken ct) =>
		await collection.InsertManyAsync(entries, cancellationToken: ct);

	/// <inheritdoc/>
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
