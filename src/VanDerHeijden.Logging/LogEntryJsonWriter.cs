using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace VanDerHeijden.Logging;

/// <summary>
/// Serializes <see cref="LogEntry"/> instances to UTF-8 JSON with <see cref="Utf8JsonWriter"/>.
/// This is the single place where the JSON field names are defined; the file, Redis and SQL writers all use it.
/// The writer appends to an internal, reusable buffer and is not thread-safe: use one instance per log writer.
/// </summary>
public sealed class LogEntryJsonWriter
{
	private static readonly JsonWriterOptions WriterOptions = new()
	{
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		SkipValidation = true
	};

	private readonly ArrayBufferWriter<byte> buffer = new(4096);
	private readonly Utf8JsonWriter json;

	/// <summary>
	/// Initializes a new <see cref="LogEntryJsonWriter"/> with an empty buffer.
	/// </summary>
	public LogEntryJsonWriter() => json = new Utf8JsonWriter(buffer, WriterOptions);

	/// <summary>Gets the UTF-8 bytes written since the last <see cref="Clear"/>.</summary>
	public ReadOnlyMemory<byte> WrittenMemory => buffer.WrittenMemory;

	/// <summary>Empties the buffer while keeping its capacity.</summary>
	public void Clear() => buffer.ResetWrittenCount();

	/// <summary>Returns a copy of the UTF-8 bytes written since the last <see cref="Clear"/>.</summary>
	public byte[] ToArray() => buffer.WrittenSpan.ToArray();

	/// <summary>Returns the JSON written since the last <see cref="Clear"/> as a string.</summary>
	public override string ToString() => Encoding.UTF8.GetString(buffer.WrittenSpan);

	/// <summary>
	/// Appends <paramref name="entry"/> as one JSON object followed by a line feed (JSON Lines).
	/// </summary>
	/// <param name="entry">The entry to serialize.</param>
	public void WriteLine(LogEntry entry)
	{
		Write(entry);
		buffer.GetSpan(1)[0] = (byte)'\n';
		buffer.Advance(1);
	}

	/// <summary>
	/// Appends <paramref name="entry"/> as one JSON object. Fields that are <see langword="null"/> are omitted.
	/// </summary>
	/// <param name="entry">The entry to serialize.</param>
	public void Write(LogEntry entry)
	{
		json.Reset();
		json.WriteStartObject();
		json.WriteString("timestamp", entry.Timestamp);
		json.WriteString("level", entry.Level.ToString());
		json.WriteNumber("eventId", entry.EventId);
		WriteIfSet("eventName", entry.EventName);
		json.WriteString("category", entry.Category);
		json.WriteString("message", entry.Message);
		WriteIfSet("messageTemplate", entry.MessageTemplate);

		if (entry.Properties is { Count: > 0 } properties)
		{
			json.WritePropertyName("properties");
			WritePropertiesObject(properties);
		}

		WriteIfSet("exception", entry.Exception);
		WriteIfSet("path", entry.Path);
		WriteIfSet("method", entry.Method);
		WriteIfSet("clientIp", entry.ClientIp);
		WriteIfSet("referer", entry.Referer);
		WriteIfSet("userAgent", entry.UserAgent);
		WriteIfSet("sessionId", entry.SessionId);
		WriteIfSet("sessionGuid", entry.SessionGuid);
		json.WriteEndObject();
		json.Flush();
	}

	/// <summary>
	/// Appends only the structured properties as one JSON object (e.g. <c>{"UserId":42}</c>).
	/// </summary>
	/// <param name="properties">The properties to serialize.</param>
	public void WriteProperties(Dictionary<string, object?> properties)
	{
		json.Reset();
		WritePropertiesObject(properties);
		json.Flush();
	}

	private void WritePropertiesObject(Dictionary<string, object?> properties)
	{
		json.WriteStartObject();
		foreach (var (key, value) in properties)
		{
			json.WritePropertyName(key);
			switch (value)
			{
				case null: json.WriteNullValue(); break;
				case string s: json.WriteStringValue(s); break;
				case bool b: json.WriteBooleanValue(b); break;
				case int i: json.WriteNumberValue(i); break;
				case long l: json.WriteNumberValue(l); break;
				case double d when double.IsFinite(d): json.WriteNumberValue(d); break;
				case decimal m: json.WriteNumberValue(m); break;
				case DateTime dt: json.WriteStringValue(dt); break;
				case Guid g: json.WriteStringValue(g); break;
				case JsonSnapshot snapshot: json.WriteRawValue(snapshot.Utf8Json.Span, skipInputValidation: true); break;
				default: json.WriteStringValue(value.ToString()); break;
			}
		}
		json.WriteEndObject();
	}

	private void WriteIfSet(string name, string? value)
	{
		if (value is not null) json.WriteString(name, value);
	}
}
