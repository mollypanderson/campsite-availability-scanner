using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CampsiteAvailabilityScanner.Models;
using System.Net.Http;

namespace CampsiteAvailabilityScanner.Services
{
    public class SlackBotService
    {
        private readonly ConcurrentDictionary<string, ConversationState> _userStates;
        private readonly string _slackBotToken;
        private readonly HttpClient _httpClient;

        public SlackBotService(string slackBotToken)
        {
            _slackBotToken = slackBotToken;
            _userStates = new ConcurrentDictionary<string, ConversationState>();
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _slackBotToken);
        }

        public async Task HandleIncomingMessageAsync(string userId, string channelId, string body)
        {
            var state = _userStates.GetOrAdd(userId, _ => new ConversationState());

            if (state.LastQuestionAsked == null)
            {
                if (body.Trim().Equals("LIST", StringComparison.OrdinalIgnoreCase))
                {
                    await PrintTrackedSitesAsync(channelId);
                }
                else
                { 
                    await ProcessPermitUrlAndAskWhichZonesAsync(channelId, body, state);
                }
            }
            else if (state.LastQuestionAsked == "ask_permit_zones")
            {
                await ProcessZoneResponseAsync(channelId, body, state);
            }
            else
            {
                await ShareInstructionsAsync(channelId, state);
            }
        }

        private async Task ProcessPermitUrlAndAskWhichZonesAsync(string channelId, string body, ConversationState state)
        {
            var apiClient = new RecreationApiClient();
            string urlCandidate = Utils.ExtractUrlFromSlackMessage(body);

            if (Uri.TryCreate(urlCandidate, UriKind.Absolute, out Uri uriResult) &&
                (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
            {
                string permitId = uriResult.Segments.Last().TrimEnd('/');
                // continue processing

                JsonObject permitSiteInfo = await apiClient.GetPermitSiteInformation(permitId) ?? new JsonObject();
                state.CurrentParkInformation = JsonSerializer.Deserialize<ParkInformation>(permitSiteInfo)!;

                await AskWhichZonesAsync(channelId, state);
            }
            else
            {
                await ShareInstructionsAsync(channelId, state);
            }
        }

        private async Task AskWhichZonesAsync(string channelId, ConversationState state)
        {
            state.LastQuestionAsked = "ask_permit_zones";

            string numberedList = string.Join("\n\t",
                state.CurrentParkInformation.ZoneOptions
                    .Select(kvp => $"{kvp.Key}) {kvp.Value}"));

            string message =
                $"Found: *{state.CurrentParkInformation.PermitSiteName}* associated with permit id {state.CurrentParkInformation.PermitId}\n\n" +
                $"Which zone(s) do you want to track?\n\t{numberedList}\n\n" +
                $"Reply with the zone numbers separated by commas (e.g., '2' or '2,4,5'). Or 'ALL'.";

            await SendMessageAsync(channelId, message);
        }

        private async Task ProcessZoneResponseAsync(string channelId, string body, ConversationState state)
        {
            if (body.Trim().Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                string[] zoneNamesToTrack = state.CurrentParkInformation.ZoneOptions.Values.ToArray();
                state.SelectedZones = zoneNamesToTrack;
                await AddPermitZonesToTrackListAsync(state);
                await SendMessageAsync(channelId, $"Got it! Tracking all zones for {state.CurrentParkInformation.PermitSiteName}.");
            }
            else
            {
                var selectedZoneNumbers = body.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (selectedZoneNumbers.Length == 0)
                {
                    await SendMessageAsync(channelId, "Please provide valid zones, or type ALL.");
                    return;
                }

                string[] zoneNamesToTrack = selectedZoneNumbers
                    .Where(num => state.CurrentParkInformation.ZoneOptions.ContainsKey(num))
                    .Select(num => state.CurrentParkInformation.ZoneOptions[num])
                    .ToArray();

                state.SelectedZones = zoneNamesToTrack;
                await AddPermitZonesToTrackListAsync(state);
                await SendMessageAsync(channelId, $"✅ Tracking zones: {string.Join(", ", state.SelectedZones)} for {state.CurrentParkInformation.PermitSiteName}.");
            }

            state.LastQuestionAsked = null;
        }

        private async Task AddPermitZonesToTrackListAsync(ConversationState state)
        {
            var apiClient = new RecreationApiClient();

            foreach (var zone in state.SelectedZones)
            {
                string[] result = await apiClient.GetCampsiteIdsForZone(state, zone);
                File.AppendAllText("trackList.json", string.Join("\n", result) + Environment.NewLine);
            }

            Console.WriteLine($"Added permit ID {state.CurrentParkInformation.PermitId} with zones {string.Join(", ", state.SelectedZones)} to tracking list.");
        }

        private async Task PrintTrackedSitesAsync(string channelId)
        {
            if (!File.Exists("trackList.json") || new FileInfo("trackList.json").Length == 0)
            {
                await SendMessageAsync(channelId, "You are not tracking any sites yet.");
                return;
            }

            var lines = File.ReadAllLines("trackList.json");
            var sites = lines
                .Select(line => JsonSerializer.Deserialize<Campsite>(line))
                .Where(c => c != null)
                .GroupBy(c => c!.PermitSite)
                .Select(g => new
                {
                    PermitSite = g.Key,
                    Zones = g.Select(c => c!.Zone).Distinct().ToArray()
                })
                .ToArray();

            string message = "You're tracking the following sites:\n\n" +
                string.Join("\n", sites.Select(s =>
                    $"*{s.PermitSite}*:\n" +
                    string.Join("\n", s.Zones.Select(z => $" - {z}"))
                ));

            await SendMessageAsync(channelId, message);
        }

        private async Task ShareInstructionsAsync(string channelId, ConversationState state)
        {
            string instructions = "That's not a valid command.\n\nOptions: \n" +
                                  "\t- Enter `LIST` to list all the sites you are tracking\n" +
                                  "\t- Enter a Recreation.gov permit URL to choose from a list of zones for that permit site to track. Example: `https://www.recreation.gov/permits/4675338`";
            state.LastQuestionAsked = null; // reset state
            await SendMessageAsync(channelId, instructions);
        }

        public async Task SendAvailabilityAlertAsync(string channelId, string message)
        {
            await SendMessageAsync(channelId, message);
        }

        private async Task SendMessageAsync(string channelId, string text)
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _slackBotToken);

            var payload = new
            {
                channel = "C09DEDCDC2E",
                text = text
            };
            var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

            // Fire-and-forget is safer if you don't need response immediately
            var response = await http.PostAsync("https://slack.com/api/chat.postMessage", content);
            var body = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Sent to Slack: {body}");
        }
    }
}
