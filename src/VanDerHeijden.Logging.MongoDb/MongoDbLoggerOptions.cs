using MongoDB.Bson;
using MongoDB.Driver;
using System.Threading.Channels;

namespace VanDerHeijden.Logging.MongoDb;

/// <summary>
/// Options for the MongoDB logger. The batching defaults are <c>BatchSize</c> 100, <c>MaxIdleMs</c> 3000 and
/// <c>FullMode</c> <see cref="BoundedChannelFullMode.DropOldest"/> (logging never blocks the application).
/// </summary>
public sealed class MongoDbLoggerOptions : BatchedLoggerOptions
{
	/// <summary>
	/// Initializes the options with the MongoDB batching defaults.
	/// </summary>
	public MongoDbLoggerOptions()
	{
		BatchSize = 100;
		MaxIdleMs = 3000;
		FullMode = BoundedChannelFullMode.DropOldest;
	}

	/// <summary>
	/// Gets or sets whether the indexes are created when the logger starts. Defaults to <see langword="true"/>.
	/// </summary>
	public bool CreateIndexes { get; set; } = true;

	/// <summary>
	/// Gets or sets the number of days after which log entries are removed by a TTL index on <c>Timestamp</c>.
	/// <see langword="null"/> (the default) keeps entries forever.
	/// </summary>
	public int? RetentionDays { get; set; }

	/// <summary>
	/// Gets or sets whether the formatted message (e.g. <c>"User 42 logged in"</c>) is stored as <c>Message</c>.
	/// Defaults to <see langword="true"/>. When <see langword="false"/>, the text can be rebuilt from
	/// <c>MessageTemplate</c> and <c>Properties</c>. The message is still stored when the template is not
	/// (no template on the entry, or <see cref="StoreMessageTemplate"/> is off), so the text is never lost.
	/// </summary>
	public bool StoreMessage { get; set; } = true;

	/// <summary>
	/// Gets or sets whether the message template (e.g. <c>"User {UserId} logged in"</c>) is stored as
	/// <c>MessageTemplate</c>. Defaults to <see langword="true"/>.
	/// </summary>
	public bool StoreMessageTemplate { get; set; } = true;
}

/// <summary>
/// Creates the indexes used to query the log collection.
/// </summary>
public static class MongoDbLogIndexes
{
	private const string TimestampIndexName = "Timestamp_-1";

	/// <summary>
	/// Ensures the log indexes exist: <c>Timestamp</c> descending (with a TTL when
	/// <see cref="MongoDbLoggerOptions.RetentionDays"/> is set), <c>Level + Timestamp</c>,
	/// <c>Category + Timestamp</c> and a wildcard index on <c>Properties.$**</c>.
	/// Safe to call repeatedly; a changed retention replaces the existing <c>Timestamp</c> index.
	/// </summary>
	/// <param name="collection">The log collection.</param>
	/// <param name="options">The logger options.</param>
	/// <param name="ct">A token that can cancel the operation.</param>
	public static async Task EnsureAsync(IMongoCollection<BsonDocument> collection, MongoDbLoggerOptions options, CancellationToken ct = default)
	{
		var keys = Builders<BsonDocument>.IndexKeys;

		var timestampIndex = new CreateIndexModel<BsonDocument>(
			keys.Descending(nameof(LogEntry.Timestamp)),
			new CreateIndexOptions
			{
				Name = TimestampIndexName,
				ExpireAfter = options.RetentionDays is { } days ? TimeSpan.FromDays(days) : null
			});

		try
		{
			await collection.Indexes.CreateOneAsync(timestampIndex, cancellationToken: ct);
		}
		catch (MongoCommandException ex) when (ex.Code is 85 or 86) // IndexOptionsConflict / IndexKeySpecsConflict
		{
			// The index exists with a different TTL: replace it.
			await collection.Indexes.DropOneAsync(TimestampIndexName, ct);
			await collection.Indexes.CreateOneAsync(timestampIndex, cancellationToken: ct);
		}

		await collection.Indexes.CreateManyAsync(
		[
			new(keys.Ascending(nameof(LogEntry.Level)).Descending(nameof(LogEntry.Timestamp))),
			new(keys.Ascending(nameof(LogEntry.Category)).Descending(nameof(LogEntry.Timestamp))),
			new(keys.Wildcard(nameof(LogEntry.Properties)))
		], ct);
	}
}
