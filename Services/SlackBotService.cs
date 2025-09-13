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
using System.Globalization;
using System.Data;
using System.Diagnostics.Eventing.Reader;

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
                    await ProcessPermitUrlAsync(channelId, urlPart, state);
                    await AskWhichStartingAreasAsync(channelId, state);
                }
                else
                { 
                    await ShareInstructionsAsync(channelId, state);
                }
            }
            else if (state.LastQuestionAsked == "asked_about_startingAreas")
            {
                await ProcessStartingAreaResponseAsync(channelId, body, state);
                await AskAboutSitesZonesAsync(channelId, state);
            }
            else if (state.LastQuestionAsked == "asked_about_siteZones")
            {
                await ProcessSitesZonesResponseAsync(channelId, body, state);
                await AskAboutDatesAsync(channelId, state);
            }
            else if (state.LastQuestionAsked == "asked_about_dates")
            {
                await ProcessDateResponseAsync(channelId, body, state);
            }
            else
            {
                await ShareInstructionsAsync(channelId, state);
            }
        }

        private async Task ProcessPermitUrlAsync(string channelId, string body, ConversationState state)
        {
            var apiClient = new RecreationApiClient();
            string urlCandidate = Utils.ExtractUrlFromSlackMessage(body);

            if (Uri.TryCreate(urlCandidate, UriKind.Absolute, out Uri uriResult) &&
                (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
            {
                string permitId = uriResult.Segments.Last().TrimEnd('/');
                PermitArea permitAreaInfo = await apiClient.GetPermitSiteInformation(permitId) ?? new PermitArea();
                state.PermitArea = permitAreaInfo;
            }
            else
            {
                await ShareInstructionsAsync(channelId, state);
                return;
            }
        }

        private async Task AskWhichStartingAreasAsync(string channelId, ConversationState state)
        {

            string numberedList = string.Join("\n\t",
                state.PermitArea!.StartingAreas.Select((area, index) => $"{index + 1}) {area.Name}"));

            string message =
                $"Found: *{state.PermitArea.Name}* associated with permit id {state.PermitArea.Id}\n\n" +
                $"Which area(s) do you want to track?\n\t{numberedList}\n\n" +
                $"Reply with the numbers separated by commas (e.g., '2' or '2,4,5'). Or 'ALL'.";

            await slackClient.SendMessageToChannelAsync(channelId, message);
            state.LastQuestionAsked = "asked_about_startingAreas";
        }

        private async Task ProcessStartingAreaResponseAsync(string channelId, string body, ConversationState state)
        {
            if (body.Trim().Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                // don't filter the list
            }
            else
            {
                PermitArea permitAreaWithFilteredStartingAreas;
                var selectedStartingAreas = body.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (selectedStartingAreas.Length == 0)
                {
                    await slackClient.SendMessageToChannelAsync
                    (channelId, "Please provide valid zones, or type ALL.");
                    return;
                }

                permitAreaWithFilteredStartingAreas = new PermitArea
                {
                    Name = state.PermitArea!.Name,
                    Id = state.PermitArea.Id,
                    StartingAreas = state.PermitArea.StartingAreas
                        .Where((area, index) => selectedStartingAreas.Contains((index + 1).ToString()))
                        .ToList()
                };
                state.PermitArea = permitAreaWithFilteredStartingAreas;
            }
        }

        private async Task AskAboutSitesZonesAsync(string channelId, ConversationState state)
        {
            state.LastQuestionAsked = "asked_about_siteZones";

            // present user with list of sites/zones - renumber the startingAreas list and do like 1A, 1B, etc for the site/zones within those
            string options = "";

            int startingAreaIndex = 0;
            foreach (StartingArea startingArea in state.PermitArea!.StartingAreas)
            {
                startingAreaIndex++;
                options += $"{startingAreaIndex}) {startingArea.Name}\n";
                string siteIndex = "A";
                foreach (Site site in startingArea.Sites)
                {
                    options += $"\t{siteIndex}) {site.Name}\n";
                    siteIndex = Utils.IncrementLabel(siteIndex);
                }
            }

            string message =
                $"Which specific sites/zones do you want to track?\n\n" +
                $"{options}\n" +
                $"Reply with a list of site number + letters separated by commas (e.g., '1A, 1B, 3A').\n" +
                $"Or reply with 'ALL' to track all sites listed.";

            await slackClient.SendMessageToChannelAsync
            (channelId, message);
        }

        private async Task ProcessSitesZonesResponseAsync(string channelId, string body, ConversationState state)
        {
            if (body.Trim().Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                // User wants to track all sites in the selected zones
                state.LastQuestionAsked = null;
            }
            else
            {
                var siteSelectionsUserInput = body.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => s.Trim().ToUpper())
                    .ToArray();

                // foreach
                // this is not done

                List<Site> selectedSites = new List<Site>();

                foreach (var selectionItem in siteSelectionsUserInput)
                {
                    // Expect sel like "1G"
                    if (selectionItem.Length < 2) continue; // invalid input

                    // Parse zone index
                    if (!int.TryParse(selectionItem.Substring(0, selectionItem.Length - 1), out int zoneNumber))
                        continue; // invalid zone number

                    char siteLetter = selectionItem[^1]; // last character
                    int zoneIndex = zoneNumber - 1; // zero-based
                    int siteIndex = siteLetter - 'A'; // 'A' -> 0, 'B' -> 1

                    // Validate indices
                    if (zoneIndex < 0 || zoneIndex >= state.PermitArea!.StartingAreas.Count) continue;

                    var startingArea = state.PermitArea.StartingAreas[zoneIndex];

                    if (siteIndex < 0 || siteIndex >= startingArea.Sites.Count) continue;

                    selectedSites.Add(startingArea.Sites[siteIndex]);
                }

                PermitArea permitAreaWithFilteredSites = Utils.FilterPermitAreaBySelectedSites(state.PermitArea!, selectedSites);

                state.LastQuestionAsked = null;
                state.PermitArea = permitAreaWithFilteredSites;
            }
        }

        private async Task AskAboutDatesAsync(string channelId, ConversationState state)
        {
            state.LastQuestionAsked = "asked_about_dates";

            string message =
                $"What dates do you want to track availability for?\n" +
                $"Reply with a list of dates in M/D format, separated by commas (e.g., '6/15,6/16,6/17').";

            await slackClient.SendMessageToChannelAsync
            (channelId, message);
        }

        private async Task ProcessDateResponseAsync(string channelId, string body, ConversationState state)
        {
            // input expected: 10/22
            string[] selectedDatesInMonthDateFormat = body.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(dateStr => DateTime.TryParse(dateStr, out DateTime date))
                .Select(dateStr => DateTime.Parse(dateStr).ToString("M/d"))
                .ToArray();

            if (selectedDatesInMonthDateFormat.Length == 0)
            {
                await slackClient.SendMessageToChannelAsync
                (channelId, "Please provide valid dates in M/D format. Example: 6/5 or 10/22");
                return;
            }

            List<DateTime> selectedDates = selectedDatesInMonthDateFormat
                .Select(dateStr =>
                {
                    DateTime dt = DateTime.ParseExact(dateStr, "M/d", CultureInfo.InvariantCulture);
                    return new DateTime(DateTime.Now.Year, dt.Month, dt.Day); // force year to 2025
                })
                .ToList();

            if (selectedDates.Count == 0)
            {
                await slackClient.SendMessageToChannelAsync
                (channelId, "No zones selected to track. Please start over.");
                state.LastQuestionAsked = null; // reset state
                return;
            }
            // add same dates to each site
            state.PermitArea!.StartingAreas
                .ForEach(sa => sa.Sites
                    .ForEach(site => site.Dates = site.Dates.Union(selectedDates).ToList()));


            await AddPermitZonesToTrackListAsync(state, state.PermitArea!);
            await slackClient.SendMessageToChannelAsync("tbd", channelId);
          //update  (channelId, $"✅ Tracking dates: {string.Join(", ", selectedDatesMonthDateFormat)} for *{state.CurrentParkInformation.PermitSiteName}* zones _{string.Join(", ", state.SelectedStartingAreas)}_.");

            state.LastQuestionAsked = null; // reset state
        }

        private async Task AddPermitZonesToTrackListAsync(ConversationState state, PermitArea permitArea)
        {
            // add to mongo
         //   Console.WriteLine($"Added {state.SelectedStartingAreas.Length} zones to track list for user {state.UserId}");
        }

        private async Task PrintTrackedSitesAsync(string channelId)
        {
            //tbd redone after mongo
            if (!File.Exists("trackList.json") || new FileInfo("trackList.json").Length == 0)
            {
                await slackClient.SendMessageToChannelAsync
                (channelId, "You are not tracking any sites yet.");
                return;
            }

            // var lines = File.ReadAllLines("trackList.json");
            // var sites = lines
            //     .Select(line => JsonSerializer.Deserialize<Site>(line))
            //     .Where(c => c != null)
            //     .GroupBy(c => c!.PermitSite)
            //     .Select(g => new
            //     {
            //         PermitSite = g.Key,
            //         Zones = g.Select(c => c!.Zone).Distinct().ToArray()
            //     })
            //     .ToArray();

            // string message = "You're tracking the following sites:\n\n" +
            //     string.Join("\n", sites.Select(s =>
            //         $"*{s.PermitSite}*:\n" +
            //         string.Join("\n", s.Zones.Select(z => $" - {z}"))
            //     ));

            // await slackClient.SendMessageToChannelAsync(channelId, message);
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
