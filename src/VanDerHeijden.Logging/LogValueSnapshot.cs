using System.Text.Json;
using System.Text.Json.Serialization;

namespace VanDerHeijden.Logging;

/// <summary>
/// Captures complex property values (objects, records, collections) as an immutable JSON snapshot at log time.
/// </summary>
internal static class LogValueSnapshot
{
	// Larger snapshots fall back to ToString(): a log property should not carry a whole object graph.
	private const int MaxSnapshotBytes = 32 * 1024;

	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		ReferenceHandler = ReferenceHandler.IgnoreCycles,
		MaxDepth = 16,
		Converters = { new JsonStringEnumConverter() }
	};

	/// <summary>
	/// Serializes <paramref name="value"/> to a <see cref="JsonElement"/> (object or array), or to a plain
	/// string/number/bool when that is what it serializes to. Falls back to <c>ToString()</c> when the value
	/// cannot be serialized or the snapshot would be too large; logging must never throw.
	/// </summary>
	public static object? Capture(object value)
	{
		try
		{
			byte[] utf8 = JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), SerializerOptions);
			if (utf8.Length <= MaxSnapshotBytes)
			{
				var reader = new Utf8JsonReader(utf8);
				var element = JsonElement.ParseValue(ref reader);
				switch (element.ValueKind)
				{
					case JsonValueKind.Object when HasContent(element):
					case JsonValueKind.Array:
						return element;
					case JsonValueKind.String:
						return element.GetString();
					case JsonValueKind.True:
					case JsonValueKind.False:
						return element.GetBoolean();
					case JsonValueKind.Number:
						return element.TryGetInt64(out long l) ? l : element.GetDouble();
				}
			}
		}
		catch { }

		try { return value.ToString(); }
		catch { return value.GetType().FullName; }
	}

	// An object without public properties serializes to {}; its ToString() says more.
	private static bool HasContent(JsonElement element)
	{
		foreach (var property in element.EnumerateObject())
			return true;
		return false;
	}
}
