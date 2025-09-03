using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using CampsiteAvailabilityScanner.Models;

public class ScheduledTaskService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            Console.WriteLine($"{DateTime.Now} Scanning for permit availability...");

            string filePath = "trackList.json"; 

            var campsitesToTrack = new List<Campsite>();

            var apiClient = new RecreationApiClient();

            foreach (var line in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var campsite = JsonSerializer.Deserialize<Campsite>(line);
                if (campsite != null)
                {
                    campsitesToTrack.Add(campsite);
                    await apiClient.GetPermitZoneAvailabilityAsync(campsite);
                }
            }
             
          //  await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
}
