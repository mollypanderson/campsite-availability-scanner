using System.Text.Json.Serialization;

namespace CampsiteAvailabilityScanner.Models
{
    public class PermitZonesAvailabilityResult
    {
        [JsonPropertyName("permitName")]
        public string PermitName { get; set; } = string.Empty;

        [JsonPropertyName("permitId")]
        public string PermitId { get; set; } = string.Empty;

        [JsonPropertyName("zones")]
        public string Zones { get; set; } = string.Empty;
    }
}
