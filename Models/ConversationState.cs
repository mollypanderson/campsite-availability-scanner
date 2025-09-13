using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace CampsiteAvailabilityScanner.Models
{
    public class ConversationState
    {
        public string UserId { get; set; } = "";
        public string? LastQuestionAsked { get; set; }

        public PermitArea? PermitArea;

        public ConversationState() { }

        public ConversationState(string userId)
        {
            UserId = userId;
        }
    }
}
