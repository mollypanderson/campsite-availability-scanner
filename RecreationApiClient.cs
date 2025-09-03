using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public class RecreationApiClient
{
    private readonly HttpClient _httpClient;

    Dictionary<string, string> laVerkinCreekNames;

    public RecreationApiClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<JsonObject?> GetPermitSiteInformation(string permitId)
    {
        string permitSiteInformationUrl = $" https://www.recreation.gov/api/permitcontent/{permitId}";

        using var response = await _httpClient.GetAsync(permitSiteInformationUrl);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync();
        JsonDocument result = await JsonDocument.ParseAsync(stream);

        string? permitSiteName = result.RootElement
            .GetProperty("payload")
            .GetProperty("addresses")
            .EnumerateArray()
            .Select(addr => addr.GetProperty("description1").GetString())
            .FirstOrDefault();

        var resultBuilder = new JsonObject();
        resultBuilder.Add("permitId", permitId);
        resultBuilder.Add("permitSiteName", permitSiteName);
        // One-liner: grab all district values into "zones"
        var zones = result.RootElement
                       .GetProperty("payload")
                       .GetProperty("divisions")
                       .EnumerateObject()
                        .Where(d => d.Value.TryGetProperty("children", out JsonElement children)
                           && children.ValueKind == JsonValueKind.Array
                           && children.GetArrayLength() > 0) // parent has non-empty children
                       .Select(d => d.Value.GetProperty("district").GetString()!)
                       .Distinct()
                       .ToArray();

        resultBuilder.Add("zones", new JsonArray(zones.Select(z => JsonValue.Create(z)).ToArray()));

        Console.WriteLine(string.Join(", ", zones));

        return resultBuilder;

    }
}