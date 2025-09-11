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
        private readonly ISlackClient slackClient;
        private readonly string channelId; 
        private readonly ConcurrentDictionary<string, ConversationState> userStates;

        public SlackBotService(ISlackClient slackClient, string channelId)
        {
            this.slackClient = slackClient;
            this.channelId = channelId;
            userStates = new ConcurrentDictionary<string, ConversationState>();
        }

        public async Task HandleIncomingMessageAsync(string userId, string channelId, string body)
        {
            var state = userStates.GetOrAdd(userId, _ => new ConversationState());

            if (state.LastQuestionAsked == null)
            {
                if (body.Trim().Equals("LIST", StringComparison.OrdinalIgnoreCase))
                {
                    await PrintTrackedSitesAsync(channelId);
                }
                else if (body.Trim().StartsWith("ADD ", StringComparison.OrdinalIgnoreCase))
                {
                    string urlPart = body.Trim().Substring(4).Trim();
                    await ProcessPermitUrlAndAskWhichZonesAsync(channelId, urlPart, state);
                }
                else
                { 
                    await ShareInstructionsAsync(channelId, state);
                }
            }
            else if (state.LastQuestionAsked == "ask_permit_zones")
            {
                await ProcessZoneResponseAsync(channelId, body, state);
                await AskDatesAsync(channelId, state);
            }
            else if (state.LastQuestionAsked == "ask_dates")
            {
                await ProcessDateResponseAsync(channelId, body, state);
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

            await slackClient.SendMessageToChannelAsync
            (channelId, message);
        }

        private async Task ProcessZoneResponseAsync(string channelId, string body, ConversationState state)
        {
            if (body.Trim().Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                string[] zoneNamesToTrack = state.CurrentParkInformation.ZoneOptions.Values.ToArray();
                state.SelectedZones = zoneNamesToTrack;
            }
            else
            {
                var selectedZoneNumbers = body.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (selectedZoneNumbers.Length == 0)
                {
                    await slackClient.SendMessageToChannelAsync
                    (channelId, "Please provide valid zones, or type ALL.");
                    return;
                }

                string[] zoneNamesToTrack = selectedZoneNumbers
                    .Where(num => state.CurrentParkInformation.ZoneOptions.ContainsKey(num))
                    .Select(num => state.CurrentParkInformation.ZoneOptions[num])
                    .ToArray();

                state.SelectedZones = zoneNamesToTrack;
            }

            state.LastQuestionAsked = null;
        }

        private async Task AskDatesAsync(string channelId, ConversationState state)
        {
            state.LastQuestionAsked = "ask_dates";

            string message =
                $"What dates do you want to track availability for?\n" +
                $"Reply with a list of dates in M/D format, separated by commas (e.g., '6/15,6/16,6/17').";

            await slackClient.SendMessageToChannelAsync
            (channelId, message);
        }

        private async Task AddPermitZonesToTrackListAsync(ConversationState state)
        {
            var apiClient = new RecreationApiClient();

            foreach (var zone in state.SelectedZones)
            {
                string[] result = await apiClient.GetCampsiteIdsForZone(state, zone);
                File.AppendAllText("trackList.json", string.Join("\n", result) + Environment.NewLine);
            }
            Console.WriteLine($"Added permit ID {state.CurrentParkInformation.PermitId} with zones {string.Join(", ", state.SelectedZones)} and dates {string.Join(", ", state.SelectedDates ?? new string[0])} to trackList.json");
        }

        private async Task ProcessDateResponseAsync(string channelId, string body, ConversationState state)
        {
            var selectedDatesMonthDateFormat = body.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(dateStr => DateTime.TryParse(dateStr, out DateTime date))
                .Select(dateStr => DateTime.Parse(dateStr).ToString("M/d"))
                .ToArray();

            if (selectedDatesMonthDateFormat.Length == 0)
            {
                await slackClient.SendMessageToChannelAsync
                (channelId, "Please provide valid dates in M/D format. Example: 6/5 or 10/22");
                return;
            }

            var selectedDatesYearFormat = selectedDatesMonthDateFormat
                .Select(dateStr => DateTime.Now.Year + DateTime.Parse(dateStr).ToString("-MM-dd"))
                .ToArray();

            state.SelectedDates = selectedDatesYearFormat;
            if (state.SelectedZones.Length == 0)
            {
                await slackClient.SendMessageToChannelAsync
                (channelId, "No zones selected to track. Please start over.");
                state.LastQuestionAsked = null; // reset state
                return;
            }
            await AddPermitZonesToTrackListAsync(state);
            await slackClient.SendMessageToChannelAsync
            (channelId, $"✅ Tracking dates: {string.Join(", ", selectedDatesMonthDateFormat)} for *{state.CurrentParkInformation.PermitSiteName}* zones _{string.Join(", ", state.SelectedZones)}_.");

            state.LastQuestionAsked = null; // reset state
        }

        private async Task PrintTrackedSitesAsync(string channelId)
        {
            if (!File.Exists("trackList.json") || new FileInfo("trackList.json").Length == 0)
            {
                await slackClient.SendMessageToChannelAsync
                (channelId, "You are not tracking any sites yet.");
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

            await slackClient.SendMessageToChannelAsync(channelId, message);
        }

        private async Task ShareInstructionsAsync(string channelId, ConversationState state)
        {
            string instructions = "That's not a valid command.\n\nOptions: \n" +
                                  "\t- Enter `LIST` to list all the sites you are tracking\n" +
                                  "\t- Enter `ADD ` + a Recreation.gov permit URL to choose from a list of zones for that permit site to track. Example: `ADD https://www.recreation.gov/permits/4675338`";
            state.LastQuestionAsked = null; // reset state
            await slackClient.SendMessageToChannelAsync(channelId, instructions);
        }

        public async Task SendAvailabilityAlertAsync(string channelId, string message)
        {
            await slackClient.SendMessageToChannelAsync(channelId, message);
        }
    }
}
