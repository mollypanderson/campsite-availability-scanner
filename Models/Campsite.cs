using System.Text.Json.Serialization;

namespace CampsiteAvailabilityScanner.Models
{
    public class Campsite
    {
        [JsonPropertyName("campsiteId")]
        public string CampsiteId { get; set; } = string.Empty;

        [JsonPropertyName("zone")]
        public string Zone { get; set; } = string.Empty;

        [JsonPropertyName("permitSite")]
        public string PermitSite { get; set; } = string.Empty;
        
        [JsonPropertyName("permitId")]
        public string PermitId { get; set; } = string.Empty;
    }

}
