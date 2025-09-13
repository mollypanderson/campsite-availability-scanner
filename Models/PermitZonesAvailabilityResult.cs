using System.Text.Json.Serialization;

namespace CampsiteAvailabilityScanner.Models
{
    public class PermitZonesAvailabilityResult
    {
        [JsonPropertyName("permitName")]
        public string PermitName { get; set; } = string.Empty;

        [JsonPropertyName("permitId")]
        public string PermitId { get; set; } = string.Empty;

        [JsonPropertyName("startingAreas")]
        public string StartingAreas { get; set; } = string.Empty;
    }
}
