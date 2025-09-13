using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class UserTrackingList
{
    [BsonId] // Primary key in MongoDB
    public string UserId { get; set; } = null!;

    [BsonElement("lastUpdated")]
    public DateTime LastUpdated { get; set; }

    [BsonElement("permitAreas")]
    public List<PermitArea> PermitAreas { get; set; } = new();
}

public class PermitArea
{
    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("id")]
    public string Id { get; set; } = null!;

    [BsonElement("startingAreas")]
    public List<StartingArea> StartingAreas { get; set; } = new();
}

public class StartingArea
{
    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("sites")]
    public List<Site> Sites { get; set; } = new();
}

public class Site
{
    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("id")]
    public string Id { get; set; } = null!;

    [BsonElement("dates")]
    public List<DateTime> Dates { get; set; } = new();
 }
