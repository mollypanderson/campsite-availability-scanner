using System.Text.Json.Serialization;

namespace CampsiteAvailabilityScanner.Models
{
    public class Zone
    {
        [JsonPropertyName("zoneName")]
        public string ZoneName { get; set; } = string.Empty;

        [JsonPropertyName("availableDates")]
        public string[] AvailableDates { get; set; } = Array.Empty<string>();
    }
}
