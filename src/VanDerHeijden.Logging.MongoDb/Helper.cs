using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace VanDerHeijden.Logging.MongoDb;

/// <summary>
/// Hulpmethoden om logs en sessies uit MongoDB op te vragen, te filteren en te verwijderen.
/// </summary>
public static class Helper
{
	/// <summary>
	/// Haalt de nieuwste logitems op, gesorteerd op <c>Timestamp</c> aflopend.
	/// </summary>
	/// <param name="logs">De collectie met logdocumenten.</param>
	/// <param name="count">Maximaal aantal items; bij 0 of minder wordt een lege lijst teruggegeven.</param>
	/// <param name="level">Optioneel: exacte naam van het logniveau (bijvoorbeeld "Error").</param>
	/// <param name="search">Optioneel: zoekterm (hoofdletterongevoelig) in Message, MessageTemplate, Category en Exception.</param>
	/// <param name="sessionId">Optioneel: zoekterm (hoofdletterongevoelig) in Path.</param>
	/// <returns>De items als dictionaries met de veldnamen van het document; een lege lijst bij een fout.</returns>
	public static async Task<List<Dictionary<string, object?>>> GetLogsAsync(
		IMongoCollection<BsonDocument> logs,
		int count, string? level, string? search, string? sessionId)
	{
		// Limit(0) betekent in MongoDB "geen limiet"; een ongeldig aantal levert dus niets op.
		if (count <= 0)
			return [];

		FilterDefinition<BsonDocument> filter = BuildLogFilter(level, search, sessionId);

		try
		{
			List<BsonDocument> documents = await logs.Find(filter)
				.Sort(Builders<BsonDocument>.Sort.Descending("Timestamp"))
				.Limit(count)
				.ToListAsync();

			return documents.ConvertAll(ToLogItem);
		}
		catch
		{
			return [];
		}
	}


	// Veldnamen en waarden volgen het opgeslagen document: Level staat als naam("Error"), niet als getal.
	// De zoektermen worden geescaped, zodat tekens als ( of [ geen regex-fout geven.
	// Let op: de UI stuurt het pad-filter mee als de parameter "sessionId"; die zoekt dus in Path.

	/// <summary>
	/// Bouwt het MongoDB-filter voor de opgegeven criteria; de criteria worden met AND gecombineerd.
	/// </summary>
	/// <param name="level">Optioneel: exacte naam van het logniveau.</param>
	/// <param name="search">Optioneel: zoekterm in Message, MessageTemplate, Category en Exception. Wordt geescaped.</param>
	/// <param name="sessionId">Optioneel: zoekterm in Path. Wordt geescaped.</param>
	/// <returns>Het filter; een leeg filter als geen criteria zijn opgegeven.</returns>
	public static FilterDefinition<BsonDocument> BuildLogFilter(
		string? level, string? search, string? sessionId)
	{
		var builder = Builders<BsonDocument>.Filter;
		var filter = builder.Empty;

		if (!string.IsNullOrEmpty(level))
			filter &= builder.Eq("Level", level);

		if (!string.IsNullOrEmpty(search))
		{
			var regex = new BsonRegularExpression(Regex.Escape(search), "i");
			filter &= builder.Or(
				builder.Regex("Message", regex),
				builder.Regex("MessageTemplate", regex),
				builder.Regex("Category", regex),
				builder.Regex("Exception", regex));
		}

		if (!string.IsNullOrEmpty(sessionId))
			filter &= builder.Regex("Path", new BsonRegularExpression(Regex.Escape(sessionId), "i"));

		return filter;
	}

	// Zelfde JSON-vorm als voorheen (PascalCase-velden); _id wordt "Id" als tekst.
	private static Dictionary<string, object?> ToLogItem(BsonDocument document)
	{
		var item = new Dictionary<string, object?>(document.ElementCount);
		foreach (BsonElement element in document)
		{
			if (element.Name == "_id")
				item["Id"] = element.Value.ToString();
			else
				item[element.Name] = BsonTypeMapper.MapToDotNetValue(element.Value);
		}
		return item;
	}


	/// <summary>
	/// Verwijdert dezelfde selectie als <see cref="GetLogsAsync"/>: alleen de nieuwste <paramref name="count"/>
	/// items die aan het filter voldoen, niet alle overeenkomende items.
	/// </summary>
	/// <param name="logs">De collectie met logdocumenten.</param>
	/// <param name="count">Maximaal aantal te verwijderen items; bij 0 of minder gebeurt er niets.</param>
	/// <param name="level">Optioneel: exacte naam van het logniveau.</param>
	/// <param name="search">Optioneel: zoekterm in Message, MessageTemplate, Category en Exception.</param>
	/// <param name="sessionId">Optioneel: zoekterm in Path.</param>
	/// <returns>Het resultaat van de verwijdering, of <c>null</c> bij een ongeldig aantal of een fout.</returns>
	public static async Task<DeleteResult?> DeleteLogsAsync(IMongoCollection<BsonDocument> logs,
		int count, string? level, string? search, string? sessionId)
	{
		// Limit(0) betekent in MongoDB "geen limiet"; voorkom dat dan alles verwijderd wordt.
		if (count <= 0)
			return null;

		FilterDefinition<BsonDocument> filter = BuildLogFilter(level, search, sessionId);

		try
		{
			// Zelfde selectie (sortering + limiet) als de lijst in de UI: alleen de getoonde
			// items worden verwijderd, niet alle items die aan het filter voldoen.
			List<BsonDocument> selected = await logs.Find(filter)
				.Sort(Builders<BsonDocument>.Sort.Descending("Timestamp"))
				.Limit(count)
				.Project(Builders<BsonDocument>.Projection.Include("_id"))
				.ToListAsync();

			// De _id is een ObjectId; die blijft ongewijzigd (geen string-conversie).
			List<BsonValue> ids = selected.ConvertAll(d => d["_id"]);

			FilterDefinition<BsonDocument> idFilter = Builders<BsonDocument>.Filter.In("_id", ids);

			return await logs.DeleteManyAsync(idFilter);
		}
		catch
		{
			return null;
		}

	}


	/// <summary>
	/// Haalt een sessiedocument op en maakt binaire velden leesbaar.
	/// </summary>
	/// <param name="sessions">De collectie met sessiedocumenten.</param>
	/// <param name="sessionId">De <c>_id</c> van de sessie.</param>
	/// <returns>
	/// Het document waarin decodeerbare binaire velden zijn vervangen door key/value-paren en vergezeld gaan van
	/// een veld <c>{naam}_Base64Size</c>; <c>null</c> als de sessie niet bestaat.
	/// </returns>
	public async static Task<BsonDocument?> GetSessionAsync(
		IMongoCollection<BsonDocument> sessions, string sessionId)
	{
		var filter = Builders<BsonDocument>.Filter.Eq("_id", sessionId);

		BsonDocument? document = await sessions.Find(filter).FirstOrDefaultAsync();

		if (document == null)
			return null;

		var result = new BsonDocument();
		foreach (BsonElement element in document)
		{
			if (element.Value.BsonType == BsonType.Binary)
			{
				byte[] bytes = element.Value.AsBsonBinaryData.Bytes;
				long base64Size = Convert.ToBase64String(bytes).Length;
				result[element.Name + "_Base64Size"] = FormatByteSize(base64Size);

				Dictionary<string, string>? decoded = TryDecodeSessionValues(bytes);
				if (decoded != null)
				{
					var sub = new BsonDocument();
					foreach (KeyValuePair<string, string> kv in decoded)
						sub[kv.Key] = kv.Value;
					result[element.Name] = sub;
					continue;
				}
			}

			result[element.Name] = element.Value;
		}
		return result;
	}

	
	private static Dictionary<string, string>? TryDecodeSessionValues(byte[] data)
	{
		for (int start = 0; start < Math.Min(64, data.Length); start++)
		{
			Dictionary<string, string>? result = TryDecodeSessionValuesFrom(data, start);
			if (result != null)
				return result;
		}

		return null;
	}

	private static Dictionary<string, string>? TryDecodeSessionValuesFrom(byte[] data, int offset)
	{
		var dict = new Dictionary<string, string>();
		int pos = offset;

		while (pos < data.Length)
		{
			while (pos < data.Length && data[pos] == 0)
				pos++;

			if (pos >= data.Length)
				break;

			int keyLen = data[pos];
			pos++;

			if (keyLen <= 0 || pos + keyLen > data.Length)
				return null;

			string key = System.Text.Encoding.UTF8.GetString(data, pos, keyLen);
			if (!IsPrintableAscii(key))
				return null;

			pos += keyLen;

			if (pos + 4 > data.Length)
				return null;

			int valueLen = (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
			pos += 4;

			if (valueLen < 0 || pos + valueLen > data.Length)
				return null;

			string value = System.Text.Encoding.UTF8.GetString(data, pos, valueLen);
			pos += valueLen;

			dict[key] = value;
		}

		return dict.Count > 0 ? dict : null;
	}

	private static string FormatByteSize(long bytes)
	{
		if (bytes < 1024)
			return $"{bytes} B";

		if (bytes < 1024 * 1024)
			return $"{bytes / 1024.0:0.##} KB";

		return $"{bytes / (1024.0 * 1024.0):0.##} MB";
	}

	private static bool IsPrintableAscii(string s)
	{
		if (s.Length == 0)
			return false;

		foreach (char c in s)
			if (c < 0x20 || c > 0x7e)
				return false;

		return true;
	}

}
