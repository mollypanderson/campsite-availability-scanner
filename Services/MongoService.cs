using MongoDB.Driver;
using System.Linq;

public class MongoService
{
    private readonly IMongoCollection<UserTrackingList> _userTrackingCollection;

    public MongoService(string connectionString, string databaseName, string collectionName)
    {
        var client = new MongoClient(connectionString);
        var database = client.GetDatabase(databaseName);

        _userTrackingCollection = database.GetCollection<UserTrackingList>(collectionName);
    }

        /// <summary>
    /// Gets the list of PermitAreas for a given userId.
    /// Returns an empty list if the user does not exist.
    /// </summary>
    public async Task<List<PermitArea>> GetPermitAreasForUserAsync(string userId)
    {
        var filter = Builders<UserTrackingList>.Filter.Eq(u => u.UserId, userId);
        var userDoc = await _userTrackingCollection.Find(filter).FirstOrDefaultAsync();

        if (userDoc == null)
        {
            return new List<PermitArea>();
        }

        return userDoc.PermitAreas ?? new List<PermitArea>();
    }
    
    /// <summary>
    /// Upserts or merges a PermitArea for a specific user.
    /// Existing PermitAreas are updated (sites/dates merged) without overwriting old content.
    /// </summary>
    public async Task UpsertOrMergePermitAreaAsync(string userId, PermitArea incomingPermitArea)
    {
        var filter = Builders<UserTrackingList>.Filter.Eq(u => u.UserId, userId);
        var userDoc = await _userTrackingCollection.Find(filter).FirstOrDefaultAsync();

        if (userDoc == null)
        {
            // User does not exist, create new document
            var newUserDoc = new UserTrackingList
            {
                UserId = userId,
                LastUpdated = DateTime.UtcNow,
                PermitAreas = new List<PermitArea> { incomingPermitArea }
            };

            await _userTrackingCollection.InsertOneAsync(newUserDoc);
            return;
        }

        // Check if PermitArea already exists
        var existingPermitArea = userDoc.PermitAreas.FirstOrDefault(pa => pa.Id == incomingPermitArea.Id);

        if (existingPermitArea == null)
        {
            // New PermitArea for this user, append
            userDoc.PermitAreas.Add(incomingPermitArea);
        }
        else
        {
            // Merge StartingAreas
            foreach (var incomingSA in incomingPermitArea.StartingAreas)
            {
                var existingSA = existingPermitArea.StartingAreas.FirstOrDefault(sa => sa.Name == incomingSA.Name);

                if (existingSA == null)
                {
                    // New StartingArea, add it
                    existingPermitArea.StartingAreas.Add(incomingSA);
                }
                else
                {
                    // Merge Sites
                    foreach (var incomingSite in incomingSA.Sites)
                    {
                        var existingSite = existingSA.Sites.FirstOrDefault(s => s.Id == incomingSite.Id);

                        if (existingSite == null)
                        {
                            // New Site, add it
                            existingSA.Sites.Add(incomingSite);
                        }
                        else
                        {
                            // Merge Dates without duplicates
                            existingSite.Dates = existingSite.Dates.Union(incomingSite.Dates).ToList();
                        }
                    }
                }
            }
        }

        // Update timestamp
        userDoc.LastUpdated = DateTime.UtcNow;

        // Replace the document in MongoDB
        await _userTrackingCollection.ReplaceOneAsync(filter, userDoc, new ReplaceOptions { IsUpsert = true });
    }
}
