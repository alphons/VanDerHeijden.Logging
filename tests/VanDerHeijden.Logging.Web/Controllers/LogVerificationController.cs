using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using VanDerHeijden.Logging.MongoDb;

namespace VanDerHeijden.Logging.Web.Controllers;

public enum OrderStatus { Pending, Shipped }

public sealed record Address(string City);

public sealed record Customer(string Name, int Number, string[] Tags, Address Address, OrderStatus Status);

/// <summary>
/// Logs one structured message and verifies the document that ends up in MongoDB.
/// GET /LogVerification returns 200 when every check passes, 500 otherwise.
/// </summary>
public class LogVerificationController(ILogger<LogVerificationController> logger, MongoDbLogTarget target) : Controller
{
	public async Task<IActionResult> Index()
	{
		var testId = Guid.NewGuid();
		var orderId = Guid.NewGuid();
		var when = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Local);
		var customer = new Customer("Alice", 7, ["vip", "nl"], new Address("Zwolle"), OrderStatus.Pending);

		logger.LogInformation(new EventId(1001, "OrderPlaced"),
			"Order {OrderId} count {Count} amount {Amount} at {When} for {Customer} status {Status} by {user.name} ({$ref}) type {Type} test {TestId}",
			orderId, 42, 12.34m, when, customer, OrderStatus.Shipped, "bob", "x", typeof(Customer), testId);

		// The batch is flushed after at most 3 seconds of inactivity.
		var filter = Builders<BsonDocument>.Filter.Eq("Properties.TestId", new BsonBinaryData(testId, GuidRepresentation.Standard));
		BsonDocument? doc = null;
		for (int attempt = 0; attempt < 40 && doc is null; attempt++)
		{
			await Task.Delay(250);
			doc = await target.Collection.Find(filter).FirstOrDefaultAsync();
		}

		var checks = new List<(string Name, bool Ok)>();
		void Check(string name, bool ok) => checks.Add((name, ok));

		Check("document stored", doc is not null);
		if (doc is not null)
		{
			var props = doc.GetValue("Properties", new BsonDocument()).AsBsonDocument;
			BsonValue P(string name) => props.GetValue(name, BsonNull.Value);

			Check("_id is ObjectId", doc["_id"].IsObjectId);
			Check("Timestamp is a recent UTC date", doc["Timestamp"].IsBsonDateTime && Math.Abs((DateTime.UtcNow - doc["Timestamp"].ToUniversalTime()).TotalMinutes) < 1);
			Check("Level is string 'Information'", doc["Level"] == "Information");
			Check("EventId is int 1001", doc["EventId"].IsInt32 && doc["EventId"] == 1001);
			Check("EventName", doc.GetValue("EventName", "") == "OrderPlaced");
			Check("Category", doc["Category"] == typeof(LogVerificationController).FullName);
			Check("MessageTemplate kept", doc.GetValue("MessageTemplate", "").AsString.StartsWith("Order {OrderId} count {Count}"));
			Check("Message formatted", doc["Message"].AsString.Contains("count 42"));
			Check("Properties is a subdocument", doc.GetValue("Properties", BsonNull.Value).IsBsonDocument);
			Check("int -> Int32", P("Count").IsInt32 && P("Count") == 42);
			Check("decimal -> Decimal128", P("Amount").IsDecimal128 && P("Amount").AsDecimal == 12.34m);
			Check("Guid -> binary subtype 4", P("OrderId") is BsonBinaryData { SubType: BsonBinarySubType.UuidStandard } bin && bin.ToGuid() == orderId);
			Check("DateTime -> UTC date", P("When").IsBsonDateTime && P("When").ToUniversalTime() == when.ToUniversalTime());
			Check("custom object -> subdocument", P("Customer") is BsonDocument c && c["Name"] == "Alice" && c["Number"].IsInt32 && c["Number"] == 7
				&& c["Tags"] is BsonArray tags && tags.Count == 2 && tags[0] == "vip" && c["Address"]["City"] == "Zwolle" && c["Status"] == "Pending");
			Check("unserializable object -> ToString()", P("Type") == typeof(Customer).ToString());
			Check("enum -> string", P("Status") == "Shipped");
			Check("'.' in key sanitized", P("user_name") == "bob" && !props.Contains("user.name"));
			Check("leading '$' in key sanitized", P("_ref") == "x");
			Check("HTTP Path captured", doc.GetValue("Path", "") == "/LogVerification");
			Check("HTTP Method captured", doc.GetValue("Method", "") == "GET");

			var indexes = await (await target.Collection.Indexes.ListAsync()).ToListAsync();
			bool HasIndex(string name) => indexes.Any(i => i["name"] == name);
			Check("index Timestamp desc", HasIndex("Timestamp_-1"));
			Check("index Level + Timestamp", HasIndex("Level_1_Timestamp_-1"));
			Check("index Category + Timestamp", HasIndex("Category_1_Timestamp_-1"));
			Check("wildcard index on Properties", HasIndex("Properties.$**_1"));
			if (target.Options.RetentionDays is { } days)
				Check("TTL on Timestamp", indexes.Any(i => i["name"] == "Timestamp_-1" && i.GetValue("expireAfterSeconds", -1).ToInt64() == days * 86400L));
		}

		var passed = checks.All(c => c.Ok);
		return StatusCode(passed ? 200 : 500, new
		{
			passed,
			checks = checks.Select(c => new { c.Name, c.Ok }),
			document = doc?.ToJson()
		});
	}
}
