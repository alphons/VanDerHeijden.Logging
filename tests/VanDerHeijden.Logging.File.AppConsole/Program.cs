
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using VanDerHeijden.Logging;
using VanDerHeijden.Logging.File;

string APPSETTINGS_FILE = "appsettings.json";

IConfigurationRoot configuration = new ConfigurationBuilder()
			.SetBasePath(Directory.GetCurrentDirectory())
			.AddJsonFile(APPSETTINGS_FILE, optional: false, reloadOnChange: true)
			.Build();

IConfigurationSection configurationSection = configuration.GetSection("CustomLogging");
using (var loggerFactory = LoggerFactory.Create(b =>
{
	if (configurationSection.GetValue<bool>("ConsoleLogging"))
		b.AddCustomLogger();
	if (configurationSection.GetValue<bool>("FileLogging"))
		b.AddFileLogger();
}))
{
	var logger = loggerFactory.CreateLogger<string>();

	logger.LogInformation("Fast logging");
}

return VerifyJsonLines() ? 0 : 1;

// Logs to a temporary directory in JSON Lines format and verifies what ends up in the .jsonl file.
static bool VerifyJsonLines()
{
	string directory = Path.Combine(Path.GetTempPath(), $"jsonl-verify-{Guid.NewGuid():N}");
	var orderId = Guid.NewGuid();
	var when = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
	var mutable = new List<int> { 1 };

	using (var factory = LoggerFactory.Create(b => b.AddFileLogger(directory, LogFormat.Json)))
	{
		var logger = factory.CreateLogger("Verify.Json");
		logger.LogInformation(new EventId(7, "Order"), "Order {OrderId} count {Count} amount {Amount} at {When} list {List} \"quoted\"",
			orderId, 42, 12.34m, when, new Snapshot(mutable));
		mutable.Add(2); // must not show up: values are snapshotted at log time
		logger.LogError(new InvalidOperationException("boom"), "Failed");
	} // disposing the factory flushes and closes the file

	var checks = new List<(string Name, bool Ok)>();
	void Check(string name, bool ok) => checks.Add((name, ok));

	string[] files = Directory.Exists(directory) ? Directory.GetFiles(directory) : [];
	Check("one log-yyyyMMdd.jsonl file (UTC date)", files.Length == 1 && Path.GetFileName(files[0]) == $"log-{DateTime.UtcNow:yyyyMMdd}.jsonl");

	if (files.Length == 1)
	{
		string[] lines = File.ReadAllLines(files[0]);
		Check("two lines, one entry per line", lines.Length == 2);
		if (lines.Length == 2)
		{
			using var first = JsonDocument.Parse(lines[0]);
			using var second = JsonDocument.Parse(lines[1]);
			var e = first.RootElement;
			var p = e.GetProperty("properties");

			Check("timestamp is UTC", e.GetProperty("timestamp").GetString()!.EndsWith('Z') && Math.Abs((DateTime.UtcNow - e.GetProperty("timestamp").GetDateTime().ToUniversalTime()).TotalMinutes) < 1);
			Check("level", e.GetProperty("level").GetString() == "Information");
			Check("eventId", e.GetProperty("eventId").GetInt32() == 7);
			Check("eventName", e.GetProperty("eventName").GetString() == "Order");
			Check("category", e.GetProperty("category").GetString() == "Verify.Json");
			Check("message", e.GetProperty("message").GetString()!.Contains("count 42") && e.GetProperty("message").GetString()!.EndsWith("\"quoted\""));
			Check("messageTemplate", e.GetProperty("messageTemplate").GetString()!.StartsWith("Order {OrderId} count {Count}"));
			Check("int property is a number", p.GetProperty("Count").ValueKind == JsonValueKind.Number && p.GetProperty("Count").GetInt32() == 42);
			Check("decimal property is a number", p.GetProperty("Amount").GetDecimal() == 12.34m);
			Check("Guid property", p.GetProperty("OrderId").GetGuid() == orderId);
			Check("DateTime property is UTC", p.GetProperty("When").GetDateTime().ToUniversalTime() == when);
			Check("custom object snapshotted at log time", p.GetProperty("List").GetString() == "1");
			Check("no exception field without exception", !e.TryGetProperty("exception", out JsonElement none));

			var error = second.RootElement;
			Check("second entry level", error.GetProperty("level").GetString() == "Error");
			Check("second entry exception", error.GetProperty("exception").GetString()!.Contains("InvalidOperationException: boom"));
			Check("second entry has no properties", !error.TryGetProperty("properties", out JsonElement noProperties));
		}
	}

	foreach (var (name, ok) in checks)
		Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}");

	try { Directory.Delete(directory, recursive: true); } catch { }

	bool passed = checks.All(c => c.Ok);
	Console.WriteLine(passed ? "JSON Lines verification passed" : "JSON Lines verification FAILED");
	return passed;
}

sealed class Snapshot(List<int> values)
{
	public override string ToString() => string.Join(",", values);
}
