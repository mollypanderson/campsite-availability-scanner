using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace CampsiteAvailabilityScanner.Models
{
    public class ConversationState
    {
        public string? LastQuestionAsked { get; set; }
        public ParkInformation? CurrentParkInformation { get; set; }
        public string[]? SelectedZones { get; set; }
    }
}
