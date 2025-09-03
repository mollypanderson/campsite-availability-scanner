using System;
using System.Collections.Concurrent;
using System.Linq;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using CampsiteAvailabilityScanner.Models;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace CampsiteAvailabilityScanner.Services
{
    public class WhatsAppBotService
    {
        private readonly string _accountSid;
        private readonly string _authToken;
        private readonly string _sandboxNumber;

        private readonly ConcurrentDictionary<string, ConversationState> _userStates;

        public WhatsAppBotService(string accountSid, string authToken, string sandboxNumber)
        {
            _accountSid = accountSid;
            _authToken = authToken;
            _sandboxNumber = sandboxNumber;

            _userStates = new ConcurrentDictionary<string, ConversationState>();

            TwilioClient.Init(_accountSid, _authToken);
        }

        public void HandleIncomingMessage(string from, string body)
        {
            var state = _userStates.GetOrAdd(from, _ => new ConversationState());

            // Conversation flow
            if (state.LastQuestionAsked == null)
            {
                //assume url and figure out which permit name
                ProcessPermitUrlAndAskWhichZones(from, body, state);

            }
            else if (state.LastQuestionAsked == "ask_permit_zones")
            {
                ProcessZoneResponse(from, body, state);
            }
            else
            {
                AskWhichZones(from, state);
            }
        }

        private async void ProcessPermitUrlAndAskWhichZones(string from, string body, ConversationState state)
        {
            var apiClient = new RecreationApiClient();

            // For simplicity, assume any non-empty message is a valid URL
            if (Uri.TryCreate(body, UriKind.Absolute, out Uri? uriResult)
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
            {
                // Extract permit ID from URL (assuming last segment)
                string permitId = uriResult.Segments.Last().TrimEnd('/');

                JsonObject permitSiteInfo = await apiClient.GetPermitSiteInformation(permitId) ?? new JsonObject();
                string permitSiteName = permitSiteInfo["permitSiteName"]?.ToString();
                Console.WriteLine($"Permit Site Name: {permitSiteName}");

                state.CurrentParkInformation = JsonSerializer.Deserialize<ParkInformation>(permitSiteInfo)!;

                AskWhichZones(from, state);
            }
            else
            {
                SendMessage(from, "Please send a valid permit URL to start tracking.");
            }
        }

        private void AskWhichZones(string from, ConversationState state)
        {
            state.LastQuestionAsked = "ask_permit_zones";
            string numberedList = string.Join("\n\t",
                state.CurrentParkInformation.ZoneOptions
                    .Select(kvp => $"{kvp.Key}) {kvp.Value}"));
            SendMessage(from, $"Found: {state.CurrentParkInformation.PermitSiteName} associated with permit id {state.CurrentParkInformation.PermitId}\n\nWhich zone(s) do you want to track?\n\t{numberedList}\n\nReply with the zone numbers separated by commas (e.g., '2' or '2,4,5'). Or 'ALL'.");
        }

        private void ProcessZoneResponse(string from, string body, ConversationState state)
        {
            if (body.Trim().Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
               // state.SelectedZones = new[] { "ALL" };
                string[] zoneNamesToTrack = state.CurrentParkInformation.ZoneOptions.Values.ToArray();
                state.SelectedZones = zoneNamesToTrack;
                AddPermitZonesToTrackList(state);
                SendMessage(from, $"Got it! Tracking all zones for {state.CurrentParkInformation.PermitSiteName}.");
            }
            else
            {
                var selectedZoneNumbers = body.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (selectedZoneNumbers.Length == 0)
                {
                    SendMessage(from, "Please provide valid zones, or type ALL.");
                    return;
                }

                string[] zoneNamesToTrack = selectedZoneNumbers
                    .Where(num => state.CurrentParkInformation.ZoneOptions.ContainsKey(num)) // only include valid keys
                    .Select(num => state.CurrentParkInformation.ZoneOptions[num])            // get the zone name
                    .ToArray();

                state.SelectedZones = zoneNamesToTrack;
                AddPermitZonesToTrackList(state);
                SendMessage(from, $"✅ Tracking zones: {string.Join(", ", state.SelectedZones)} for {state.CurrentParkInformation.PermitSiteName}.");
            }

            state.LastQuestionAsked = null; // reset after response
        }

        private async Task AddPermitZonesToTrackList(ConversationState state)
        {
            var apiClient = new RecreationApiClient();
            // for each zone in zones, find the division id using the parkInfo json and call apiClient.GetPermitZoneAvailabilityAsync
            foreach (var zone in state.SelectedZones)
            {
                // for example la verkin creek
                // get the list of zone campsite ids for the given zone
                string[] result = await apiClient.GetCampsiteIdsForZone(state, zone);
                File.AppendAllText("trackList.json", string.Join("\n", result) + Environment.NewLine);

            }
            Console.WriteLine($"Added permit ID {state.CurrentParkInformation.PermitId} with zones {string.Join(", ", state.SelectedZones)} to tracking list.");
        }

        // private bool ScanForAvailability(list)
        // {
        //     // loop through each permit zone in the list and call apiClient.GetPermitZoneAvailabilityAsync
        //     // if any availability is found, return true
        //     JsonObject result = await apiClient.GetPermitZoneAvailabilityAsync(zone);
        //     return false;
        // }

        private void SendMessage(string to, string message)
        {
            MessageResource.Create(
                from: new PhoneNumber(_sandboxNumber),
                to: new PhoneNumber(to),
                body: message
            );
        }
    }
}
