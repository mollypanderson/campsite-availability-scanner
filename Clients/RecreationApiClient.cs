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

    public async Task<PermitArea?> GetPermitSiteInformation(string permitId)
    {
        string permitSiteInformationUrl = $"https://www.recreation.gov/api/permitcontent/{permitId}";

        using var response = await _httpClient.GetAsync(permitSiteInformationUrl);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var result = await JsonDocument.ParseAsync(stream);

        var payload = result.RootElement.GetProperty("payload");

        // Extract permit-level info
        string permitSiteName = payload.GetProperty("name").GetString()!;
        string permitIdFromJson = payload.GetProperty("divisions")
            .EnumerateObject().First().Value.GetProperty("permit_id").GetString()!;

        // Group divisions by district
        var divisions = payload.GetProperty("divisions")
            .EnumerateObject()
            .Select(d => d.Value)
            .Where(d =>
                d.TryGetProperty("district", out JsonElement districtProp)
                && !string.IsNullOrWhiteSpace(districtProp.GetString())
                && (!d.TryGetProperty("is_hidden", out JsonElement hidden) || !hidden.GetBoolean())
            )
            .GroupBy(d => d.GetProperty("district").GetString()!);

        // Build StartingAreas
        var startingAreas = divisions.Select(group => new StartingArea
        {
            Name = group.Key,
            Sites = group.Select(d => new Site
            {
                Name = d.GetProperty("name").GetString()!,
                Id = d.GetProperty("id").GetString()!,
                Dates = new List<DateTime>() 
            }).ToList()
        }).ToList();

        // Build PermitArea object
        var permitArea = new PermitArea
        {
            Name = permitSiteName,
            Id = permitIdFromJson,
            StartingAreas = startingAreas
        };

        return permitArea;
    }

    public async Task<List<string>> GetPermitZoneAvailabilityAsync(PermitArea permitArea, Site site)
    {
        List<string> availableDates = new List<string>();
        // JsonObject availabilityResults = new JsonObject();
       // need to fix this url so the second variable refers to a specific campsite id
        string url = $"https://www.recreation.gov/api/permititinerary/{permitArea.Id}/division/{site.Id}/availability/month?month=9&year=2025";

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
                     //   if (campsite.Dates.Contains(item.Name))
                     //   {
                     //       availableDates.Add(item.Name); 
                      //  }
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