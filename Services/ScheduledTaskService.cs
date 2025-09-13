using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using CampsiteAvailabilityScanner.Models;
using CampsiteAvailabilityScanner.Services;
using System.Text.Json.Nodes;
using System.Text;

public class ScheduledTaskService : BackgroundService
{
    private string channelId = Utils.ReadSecret("CHANNEL_ID")!;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine($"{DateTime.Now} Scanning for permit availability...");

            string filePath = "trackList.json";

        //    var campsitesToTrack = new List<Campsite>();

            HashSet<string> permitSites = new HashSet<string>();

            foreach (var line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

           //     var campsite = JsonSerializer.Deserialize<Campsite>(line);
            //    if (campsite != null)
                {
            //        campsitesToTrack.Add(campsite);
                }
            }

            var messageBuilder = new StringBuilder();

            // Group by unique permit site name
        //    var groupedBySite = campsitesToTrack
           //     .GroupBy(c => c.PermitSite);

          //  foreach (var group in groupedBySite)
            {
                // Call helper for each permit site
             //   string siteResult = await BuildResultForPermitSite(group.ToList());

             //   if (!string.IsNullOrWhiteSpace(siteResult))
                {

                    messageBuilder.AppendLine($":rotating_light::camping: *Permits available!*\n");
                //    messageBuilder.AppendLine(siteResult);

                    string slackBotToken = Utils.ReadSecret("SLACK_BOT_TOKEN")!;
                    ISlackClient slackClient = new SlackClient(slackBotToken);

                    SlackBotService slackBotService = new SlackBotService(slackClient, channelId);
                    await slackBotService.SendAvailabilityAlertAsync(channelId, $"{messageBuilder.ToString()}");
                    messageBuilder.Clear();
                }

            }

            await Task.Delay(TimeSpan.FromMinutes(50), stoppingToken);
            return;
        }
    }

  //  private async Task<string> BuildResultForPermitSite(List<Site> campsitesForPermitSite)
  //  {
    //     // need to fix
    //     bool anyAvailability = false;
    //     HashSet<string> combinedDatesForZones = new HashSet<string>();

    //     PermitZonesAvailabilityResult permitZonesAvailabilityResult = new PermitZonesAvailabilityResult
    //     {
    //      //   PermitId = campsitesForPermitSite.First().PermitSite,
    //        // PermitName = campsitesForPermitSite.First().PermitSite,
    //       //  StartingAreas = string.Join(", ", campsitesForPermitSite.Select(c => c.Zone).Distinct())
    //     };

    //     var apiClient = new RecreationApiClient();
    //   //  var siteName = campsitesForPermitSite.First().PermitSite;
    //     var builder = new StringBuilder();

    //  //   builder.AppendLine($"*{siteName}*");

    //     foreach (var campsite in campsitesForPermitSite)
    //     {
    //         List<string> availableDatesForCampsite = await apiClient.GetPermitZoneAvailabilityAsync(permitarea campsite);
    //         foreach (var date in availableDatesForCampsite)
    //         {
    //             combinedDatesForZones.Add(date);
    //         }

    //     }

    //     // Sort dates
    //     var sortedDates = combinedDatesForZones
    //         .Select(date => DateTime.Parse(date))
    //         .OrderBy(date => date)
    //         .Select(date => date.ToString("M/d"))
    //         .ToList();

    //     foreach (var zone in permitZonesAvailabilityResult.StartingAreas.Split(", "))
    //     {
    //         if (combinedDatesForZones.Count > 0)
    //         {
    //             builder.AppendLine($" - {zone}: {string.Join(", ", sortedDates)}");
    //             anyAvailability = true;
    //         }

    //     }

    //     builder.AppendLine(); // spacing after each site

    //     return anyAvailability ? builder.ToString() : string.Empty;
  //  }


}

