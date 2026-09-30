using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;

namespace VanDerHeijden.Logging.MongoDb;

/// <summary>
/// Converts a <see cref="LogEntry"/> into the <see cref="BsonDocument"/> stored in MongoDB.
/// The driver only ever sees finished BSON values, never a dictionary of arbitrary objects.
/// </summary>
public static class LogEntryBsonConverter
{
	/// <summary>
	/// Builds the MongoDB document for <paramref name="entry"/> and assigns its <c>_id</c>.
	/// Fields that are <see langword="null"/> are omitted.
	/// </summary>
	/// <param name="entry">The entry to convert.</param>
	/// <param name="options">
	/// Controls whether <c>Message</c> and <c>MessageTemplate</c> are stored; <see langword="null"/> stores both.
	/// </param>
	/// <returns>The document to insert.</returns>
	public static BsonDocument ToDocument(LogEntry entry, MongoDbLoggerOptions? options = null)
	{
		var doc = new BsonDocument
		{
			{ "_id", ObjectId.GenerateNewId() },
			{ nameof(LogEntry.Timestamp), new BsonDateTime(entry.Timestamp.ToUniversalTime()) },
			{ nameof(LogEntry.Level),     entry.Level.ToString() },
			{ nameof(LogEntry.EventId),   entry.EventId },
			{ nameof(LogEntry.Category),  entry.Category }
		};

		// The message is kept whenever the template is not stored (switched off or absent), so the text is never lost.
		bool storeTemplate = options?.StoreMessageTemplate != false && entry.MessageTemplate is not null;
		if (options?.StoreMessage != false || !storeTemplate)
			doc.Add(nameof(LogEntry.Message), entry.Message);

		AddIfSet(doc, nameof(LogEntry.EventName), entry.EventName);
		if (storeTemplate)
			doc.Add(nameof(LogEntry.MessageTemplate), entry.MessageTemplate);

		if (entry.Properties is { Count: > 0 } properties)
		{
			var props = new BsonDocument();
			foreach (var (key, value) in properties)
				props[SanitizeKey(key)] = ToBsonValue(value);
			doc.Add(nameof(LogEntry.Properties), props);
		}

		AddIfSet(doc, nameof(LogEntry.Exception), entry.Exception);
		AddIfSet(doc, nameof(LogEntry.Path), entry.Path);
		AddIfSet(doc, nameof(LogEntry.Method), entry.Method);
		AddIfSet(doc, nameof(LogEntry.ClientIp), entry.ClientIp);
		AddIfSet(doc, nameof(LogEntry.Referer), entry.Referer);
		AddIfSet(doc, nameof(LogEntry.UserAgent), entry.UserAgent);
		AddIfSet(doc, nameof(LogEntry.SessionId), entry.SessionId);
		AddIfSet(doc, nameof(LogEntry.SessionGuid), entry.SessionGuid);
		return doc;
	}

	/// <summary>
	/// Maps a property value to BSON: string/bool/int/long/double natively, <see cref="DateTime"/> as UTC,
	/// <see cref="decimal"/> as Decimal128, <see cref="Guid"/> as standard (subtype 4) binary, enums by name,
	/// <see langword="null"/> as BSON null, object snapshots (<see cref="JsonSnapshot"/>) as subdocuments and arrays,
	/// and anything else as its string representation.
	/// </summary>
	/// <param name="value">The value to map.</param>
	/// <returns>The BSON value.</returns>
	public static BsonValue ToBsonValue(object? value) => value switch
	{
		null => BsonNull.Value,
		string s => new BsonString(s),
		bool b => (BsonBoolean)b,
		int i => new BsonInt32(i),
		long l => new BsonInt64(l),
		double d => new BsonDouble(d),
		DateTime dt => new BsonDateTime(dt.ToUniversalTime()),
		decimal m => new BsonDecimal128(m),
		Guid g => new BsonBinaryData(g, GuidRepresentation.Standard),
		Enum e => new BsonString(e.ToString()),
		JsonSnapshot snapshot => ToBsonValue(snapshot.ToElement()),
		_ => new BsonString(value.ToString() ?? string.Empty)
	};

	// Object snapshots become subdocuments and arrays, so nested values can be queried (Properties.Customer.Name).
	private static BsonValue ToBsonValue(JsonElement element)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.Object:
				var doc = new BsonDocument();
				foreach (var property in element.EnumerateObject())
					doc[SanitizeKey(property.Name)] = ToBsonValue(property.Value);
				return doc;
			case JsonValueKind.Array:
				var array = new BsonArray(element.GetArrayLength());
				foreach (var item in element.EnumerateArray())
					array.Add(ToBsonValue(item));
				return array;
			case JsonValueKind.String:
				return new BsonString(element.GetString() ?? string.Empty);
			case JsonValueKind.Number:
				if (element.TryGetInt32(out int i)) return new BsonInt32(i);
				if (element.TryGetInt64(out long l)) return new BsonInt64(l);
				return new BsonDouble(element.GetDouble());
			case JsonValueKind.True:
				return BsonBoolean.True;
			case JsonValueKind.False:
				return BsonBoolean.False;
			default:
				return BsonNull.Value;
		}
	}

	/// <summary>
	/// Makes a property name safe to use as a MongoDB field name: every <c>'.'</c> and a leading <c>'$'</c>
	/// are replaced with <c>'_'</c>.
	/// </summary>
	/// <param name="key">The property name.</param>
	/// <returns>The sanitized field name.</returns>
	public static string SanitizeKey(string key)
	{
		if (key.Length == 0) return "_";
		if (key[0] != '$' && !key.Contains('.')) return key;

		return string.Create(key.Length, key, static (span, source) =>
		{
			source.CopyTo(span);
			if (span[0] == '$') span[0] = '_';
			span.Replace('.', '_');
		});
	}

	private static void AddIfSet(BsonDocument doc, string name, string? value)
	{
		if (value is not null) doc.Add(name, value);
	}
}

/// <summary>
/// Writes batches of log entries to a MongoDB collection using an unordered <c>InsertManyAsync</c>.
/// The entries are converted to BSON here, on the background writer thread, not in the logging call.
/// </summary>
/// <param name="collection">The MongoDB collection that receives log entries.</param>
/// <param name="options">Controls which message fields are stored; <see langword="null"/> uses the defaults.</param>
public sealed class MongoDbLogWriter(IMongoCollection<BsonDocument> collection, MongoDbLoggerOptions? options = null) : IBatchedLogWriter<LogEntry>
{
	private static readonly InsertManyOptions InsertOptions = new() { IsOrdered = false };

	// The documents of the batch in flight. A retried batch reuses them, so every entry keeps its _id.
	private readonly List<BsonDocument> documents = [];
	private LogEntry? pendingFirst;
	private LogEntry? pendingLast;

	/// <summary>
	/// Inserts all entries in the batch into the MongoDB collection. The insert is unordered, so one
	/// rejected document does not stop the rest. Duplicate-key errors are ignored: they mean the document
	/// was already stored by an earlier attempt of the same batch.
	/// </summary>
	/// <param name="entries">The log entries to insert.</param>
	/// <param name="ct">A token that can cancel the operation.</param>
	public async Task WriteBatchAsync(List<LogEntry> entries, CancellationToken ct)
	{
		if (entries.Count == 0) return;

		bool isRetry = documents.Count == entries.Count
			&& ReferenceEquals(pendingFirst, entries[0])
			&& ReferenceEquals(pendingLast, entries[^1]);

		if (!isRetry)
		{
			documents.Clear();
			foreach (var entry in entries)
				documents.Add(LogEntryBsonConverter.ToDocument(entry, options));
			pendingFirst = entries[0];
			pendingLast = entries[^1];
		}

		try
		{
			await collection.InsertManyAsync(documents, InsertOptions, ct);
		}
		catch (MongoBulkWriteException<BsonDocument> ex) when (
			ex.WriteConcernError is null &&
			ex.WriteErrors.All(e => e.Category == ServerErrorCategory.DuplicateKey))
		{
		}

		documents.Clear();
		pendingFirst = pendingLast = null;
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
