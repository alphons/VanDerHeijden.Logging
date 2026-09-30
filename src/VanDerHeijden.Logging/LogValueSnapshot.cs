using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VanDerHeijden.Logging;

/// <summary>
/// A snapshot of a complex log property value (object, record or collection).
/// At log time only a shallow clone of the value is taken; the JSON is produced lazily, on the background
/// writer thread, the first time <see cref="Utf8Json"/> is read.
/// </summary>
public sealed class JsonSnapshot
{
	// Larger snapshots fall back to ToString(): a log property should not carry a whole object graph.
	private const int MaxSnapshotBytes = 32 * 1024;

	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		ReferenceHandler = ReferenceHandler.IgnoreCycles,
		MaxDepth = 16,
		Converters = { new JsonStringEnumConverter() }
	};

	private object? source;
	private byte[]? utf8;

	/// <summary>
	/// Creates a snapshot from JSON that has already been serialized.
	/// </summary>
	/// <param name="utf8Json">The UTF-8 encoded JSON value.</param>
	public JsonSnapshot(byte[] utf8Json) => utf8 = utf8Json;

	private JsonSnapshot(object source, bool deferred) => this.source = source;

	internal static JsonSnapshot Deferred(object clone) => new(clone, deferred: true);

	/// <summary>
	/// Gets the UTF-8 encoded JSON. Usually an object or array; a JSON string holding the <c>ToString()</c>
	/// text when the value cannot be serialized, has no public properties or exceeds 32 KB of JSON.
	/// </summary>
	public ReadOnlyMemory<byte> Utf8Json => utf8 ??= Serialize();

	/// <summary>Parses the snapshot into a <see cref="JsonElement"/>.</summary>
	public JsonElement ToElement()
	{
		var reader = new Utf8JsonReader(Utf8Json.Span);
		return JsonElement.ParseValue(ref reader);
	}

	/// <summary>Returns the JSON text.</summary>
	public override string ToString() => Encoding.UTF8.GetString(Utf8Json.Span);

	private byte[] Serialize()
	{
		object value = source!;
		byte[]? result = null;
		try
		{
			byte[] json = JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), SerializerOptions);

			// An object without public properties serializes to {}; its ToString() says more.
			if (json.Length <= MaxSnapshotBytes && !(json.Length == 2 && json[0] == (byte)'{'))
				result = json;
		}
		catch { }

		result ??= JsonSerializer.SerializeToUtf8Bytes(LogValueSnapshot.SafeToString(value));
		source = null;
		return result;
	}
}

/// <summary>
/// Captures complex property values on the logging thread as cheaply as possible.
/// </summary>
internal static class LogValueSnapshot
{
	private static readonly ConcurrentDictionary<Type, bool> Cloneable = new();

	[UnsafeAccessor(UnsafeAccessorKind.Method, Name = "MemberwiseClone")]
	private static extern object MemberwiseClone(object target);

	/// <summary>
	/// Returns a <see cref="JsonSnapshot"/> holding a shallow clone of <paramref name="value"/>, to be serialized
	/// later on the writer thread. Top-level members are frozen at log time; nested objects and collections are
	/// shared with the original. Values that must not be cloned (resources, types, delegates, exceptions) are
	/// captured as their <c>ToString()</c> text instead. Never throws.
	/// </summary>
	public static object? Capture(object value)
	{
		try
		{
			Type type = value.GetType();

			// A boxed struct is already a private copy made for this log call.
			if (type.IsValueType) return JsonSnapshot.Deferred(value);

			if (Cloneable.GetOrAdd(type, IsCloneable))
				return JsonSnapshot.Deferred(value is Array array ? array.Clone() : MemberwiseClone(value));
		}
		catch { }

		return SafeToString(value);
	}

	public static string? SafeToString(object value)
	{
		try { return value.ToString(); }
		catch { return value.GetType().FullName; }
	}

	// Cloning an object that owns a resource would duplicate its handle and run its finalizer twice,
	// and clones of runtime objects (types, delegates, tasks) are meaningless or harmful.
	private static bool IsCloneable(Type type)
	{
		if (typeof(IDisposable).IsAssignableFrom(type) || typeof(IAsyncDisposable).IsAssignableFrom(type)) return false;
		if (typeof(Delegate).IsAssignableFrom(type) || typeof(MemberInfo).IsAssignableFrom(type)) return false;
		if (typeof(Exception).IsAssignableFrom(type) || typeof(Task).IsAssignableFrom(type)) return false;
		if (typeof(Assembly).IsAssignableFrom(type) || typeof(Module).IsAssignableFrom(type)) return false;
		if (typeof(Thread).IsAssignableFrom(type) || typeof(WaitHandle).IsAssignableFrom(type)) return false;

		var finalizer = type.GetMethod("Finalize", BindingFlags.Instance | BindingFlags.NonPublic, Type.EmptyTypes);
		return finalizer is null || finalizer.DeclaringType == typeof(object);
	}
}
