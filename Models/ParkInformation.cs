using System.Text.Json.Serialization;

namespace CampsiteAvailabilityScanner.Models
{
    public class ParkInformation
    {
        [JsonPropertyName("permitId")]
        public string PermitId { get; set; } = string.Empty;

        [JsonPropertyName("permitSiteName")]
        public string PermitSiteName { get; set; } = string.Empty;

        [JsonPropertyName("zoneOptions")]
        public Dictionary<string, string> ZoneOptions { get; set; } = new();

        [JsonPropertyName("zoneNamesAndIds")]
        public Dictionary<string, string> ZoneNamesAndIds { get; set; } = new();
        

    }
}
