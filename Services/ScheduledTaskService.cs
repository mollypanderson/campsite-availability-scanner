using CampsiteAvailabilityScanner.Models;
using CampsiteAvailabilityScanner.Services;
using System.Text;

public class ScheduledTaskService : BackgroundService
{
  private string channelId = Utils.ReadSecret("CHANNEL_ID")!;

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      string mongoUri = Utils.ReadSecret("MONGO_URI")!;
      var mongoService = new MongoService(
          mongoUri,
          "campsite-tracking-db",
          "user-campsite-tracking"
      );

      Console.WriteLine($"{DateTime.Now} Scanning for permit availability...");

      List<UserTrackingList> allUsers = await mongoService.GetAllUserListsAsync();

      foreach (var user in allUsers)
      {
        await scanForAvailabilityForUser(user);
        Console.WriteLine($"UserId: {user.UserId}, PermitAreas: {user.PermitAreas.Count}");
      }
      await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
      return;
    }
  }

  protected async Task scanForAvailabilityForUser(UserTrackingList user)
  {
    var messageBuilder = new StringBuilder();
    foreach (PermitArea permitArea in user.PermitAreas)
    {
      string permitAreaName = permitArea.Name;
      string permitId = permitArea.Id;

      List<Site> sitesToScan = permitArea.StartingAreas
      .SelectMany(sa => sa.Sites)
      .ToList();

      string siteResult = await BuildResultForPermitSite(permitAreaName, permitId, sitesToScan);

      if (!string.IsNullOrWhiteSpace(siteResult))
      {

        messageBuilder.AppendLine($":rotating_light::camping: *Permits available!*\n");
        messageBuilder.AppendLine(siteResult);

        string slackBotToken = Utils.ReadSecret("SLACK_BOT_TOKEN")!;
        ISlackClient slackClient = new SlackClient(slackBotToken);

        SlackBotService slackBotService = new SlackBotService(slackClient, channelId, null);
        await slackBotService.SendAvailabilityAlertAsync(channelId, $"{messageBuilder.ToString()}");
        messageBuilder.Clear();
      }
    }
  }

   private async Task<string> BuildResultForPermitSite(string permitAreaName, string permitId, List<Site> campsitesForPermitSite)
   {
      bool anyAvailability = false;
      HashSet<string> combinedDatesForZones = new HashSet<string>();

      PermitZonesAvailabilityResult permitZonesAvailabilityResult = new PermitZonesAvailabilityResult
      {
          PermitId = permitId,
          PermitName = permitAreaName,
          StartingAreas = string.Join(", ", campsitesForPermitSite.Select(c => c.Name).Distinct())
      };

      var apiClient = new RecreationApiClient();
      var siteName = campsitesForPermitSite.First().Name;
      var builder = new StringBuilder();

      builder.AppendLine($"*{permitAreaName}*");

      foreach (var campsite in campsitesForPermitSite)
      {
          List<string> availableDatesForCampsite = await apiClient.GetPermitZoneAvailabilityAsync(permitAreaName, permitId, campsite);
          foreach (var date in availableDatesForCampsite)
          {
              combinedDatesForZones.Add(date);
          }

      }

      // Sort dates
      var sortedDates = combinedDatesForZones
          .Select(date => DateTime.Parse(date))
          .OrderBy(date => date)
          .Select(date => date.ToString("M/d"))
          .ToList();

      foreach (var zone in permitZonesAvailabilityResult.StartingAreas.Split(", "))
      {
          if (combinedDatesForZones.Count > 0)
          {
              builder.AppendLine($" - {zone}: _{string.Join(", ", sortedDates)}_");
              anyAvailability = true;
          }

      }

      builder.AppendLine(); 

      return anyAvailability ? builder.ToString() : string.Empty;
   }
}

