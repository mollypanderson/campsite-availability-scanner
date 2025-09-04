using System;
using System.Collections;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CampsiteAvailabilityScanner.Models;
using Twilio.Rest.Serverless.V1.Service.Environment;

public class RecreationApiClient
{
    private readonly HttpClient _httpClient;

    public RecreationApiClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<JsonObject?> GetPermitSiteInformation(string permitId)
    {
        string permitSiteInformationUrl = $"https://www.recreation.gov/api/permitcontent/{permitId}";

        using var response = await _httpClient.GetAsync(permitSiteInformationUrl);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync();
        JsonDocument result = await JsonDocument.ParseAsync(stream);

        string? permitSiteName = result.RootElement
            .GetProperty("payload")
            .GetProperty("name").GetString();

        var resultBuilder = new JsonObject();
        resultBuilder.Add("permitId", permitId);
        resultBuilder.Add("permitSiteName", permitSiteName);
        // One-liner: grab all district values into "zones"
        var zoneOptions = result.RootElement
                       .GetProperty("payload")
                       .GetProperty("divisions")
                       .EnumerateObject()
                        .Where(d => d.Value.TryGetProperty("children", out JsonElement children)
                           && children.ValueKind == JsonValueKind.Array
                           && children.GetArrayLength() > 0) // parent has non-empty children
                       .Select(d => d.Value.GetProperty("district").GetString()!)
                       .Distinct()
                        .Select((district, index) => new KeyValuePair<string, string>($"{index + 1}", district))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        resultBuilder.Add("zoneOptions", new JsonObject(zoneOptions.Select(kvp => new KeyValuePair<string, JsonNode?>(kvp.Key, JsonValue.Create(kvp.Value))).ToArray()));

        Console.WriteLine(string.Join(", ", zoneOptions));

        return resultBuilder;

    }

    public async Task<string[]> GetCampsiteIdsForZone(ConversationState state, string zone)
    {
        ArrayList arrayList = new ArrayList();
        string permitSiteInformationUrl = $"https://www.recreation.gov/api/permitcontent/{state.CurrentParkInformation.PermitId}";

        using var response = await _httpClient.GetAsync(permitSiteInformationUrl);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync();
        JsonDocument result = await JsonDocument.ParseAsync(stream);

        var divisions = result.RootElement
            .GetProperty("payload")
            .GetProperty("divisions");

        foreach (var division in divisions.EnumerateObject())
        {
            string divisionId = division.Value.GetProperty("id").GetString()!;
            string districtName = division.Value.GetProperty("district").GetString()!;

            if (districtName == zone)
            {
                // add division.id to array list to return
                var json = new JsonObject
                {
                    ["campsiteId"] = divisionId,
                    ["zone"] = districtName,
                    ["permitSite"] = state.CurrentParkInformation.PermitSiteName,
                    ["permitId"] = state.CurrentParkInformation.PermitId,
                    ["dates"] = new JsonArray(state.SelectedDates.Select(date => (JsonNode)date).ToArray())

                };
                arrayList.Add(json.ToJsonString());
            }
        }

        return (string[])arrayList.ToArray(typeof(string));

    }

    public async Task<List<string>> GetPermitZoneAvailabilityAsync(Campsite campsite)
    {
        List<string> availableDates = new List<string>();
       // JsonObject availabilityResults = new JsonObject();
        string url = $"https://www.recreation.gov/api/permititinerary/{campsite.PermitId}/division/{campsite.CampsiteId}/availability/month?month=9&year=2025";

        using var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync();
        JsonDocument result = await JsonDocument.ParseAsync(stream);

        if (result.RootElement.TryGetProperty("payload", out JsonElement payload) &&
            payload.TryGetProperty("quota_type_maps", out JsonElement quotaMaps))
        {
            if (quotaMaps.TryGetProperty("ConstantQuotaUsageDaily", out JsonElement constantQuotaUsageDaily))
            {
                // ✅ Property exists, you can safely use it
                // e.g., parse availability data
                foreach (var item in constantQuotaUsageDaily.EnumerateObject())
                {
                    if (item.Value.GetProperty("show_walkup").GetBoolean() == false
                        && item.Value.GetProperty("is_hidden").GetBoolean() == false
                        && item.Value.GetProperty("remaining").GetInt32() > 0)
                    {
                        if (campsite.Dates.Contains(item.Name))
                        {
                            availableDates.Add(item.Name); 
                        }
                    }
                }
            }
            else
            {
                // ❌ Property missing — log what you got instead
                Console.WriteLine("ConstantQuotaUsageDaily property not found. quotaMaps JSON:");
                Console.WriteLine(quotaMaps.ToString());
            }
        }
        else
        {
            // ❌ Either "payload" or "quota_type_maps" missing
            Console.WriteLine("Payload or quota_type_maps property not found. Full JSON response:");
            Console.WriteLine(result.RootElement.ToString());
        }
        return availableDates;
    }
}