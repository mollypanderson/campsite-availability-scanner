using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using CampsiteAvailabilityScanner.Services;
using System.Text.Json.Nodes;

class Program
{
    private static readonly HashSet<string> _processedEventIds = new();

    static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddHostedService<ScheduledTaskService>();
        string port = Environment.GetEnvironmentVariable("PORT") ?? "5167";
        builder.WebHost.UseUrls($"http://localhost:{port}"); // HTTP only

        var app = builder.Build();

        // Load environment variables
        DotNetEnv.Env.Load();
        string env = Environment.GetEnvironmentVariable("ENV") ?? "development";
        string slackBotToken = Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN")!;
        string slackSigningSecret = Environment.GetEnvironmentVariable("SLACK_SIGNING_SECRET")!;

        Console.WriteLine($"Running in {env} mode");

        var slackBotService = new SlackBotService(slackBotToken);

        app.MapPost("/slack/events", async (HttpRequest request) =>
        {
            string signingSecret = Environment.GetEnvironmentVariable("SLACK_SIGNING_SECRET")!;

            if (!SlackRequestVerifier.VerifyRequest(request, signingSecret))
            {
                return Results.StatusCode(401); 
            }

            try
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync();

                Console.WriteLine($"Received Slack event: {body}");

                var json = JsonNode.Parse(body)!;
                string eventType = json["type"]?.ToString() ?? "";
                string eventId = json["event_id"]?.ToString() ?? "";

                // Slack URL verification challenge
                if (eventType == "url_verification")
                {
                    return Results.Ok(new { challenge = json["challenge"]!.ToString() });
                }

                // Avoid processing the same event multiple times
                if (!_processedEventIds.Add(eventId))
                    return Results.Ok();

                var eventProp = json["event"]!;
                bool hidden = eventProp["hidden"]?.GetValue<bool>() ?? false;

                // Ignore hidden/unfurl messages
                if (hidden)
                    return Results.Ok();

                // Determine the actual message element
                var messageElement = eventProp["subtype"]?.ToString() == "message_changed"
                    ? eventProp["message"]!
                    : eventProp;

                // Ignore bot messages
                if (messageElement["bot_id"] != null || messageElement["user"] == null)
                    return Results.Ok();

                string channelId = "C09DEDCDC2E";
                string userId = messageElement["user"]!.ToString();
                string text = messageElement["text"]!.ToString();

                // Clean Slack formatting: <url|url> -> url
                if (text.StartsWith("<") && text.Contains("|"))
                {
                    int pipe = text.IndexOf('|');
                    int end = text.IndexOf('>', pipe);
                    if (pipe > 0 && end > 0)
                        text = text.Substring(pipe + 1, end - pipe - 1);
                }

                // Forward to your SlackBotService
                await slackBotService.HandleIncomingMessageAsync(channelId, userId, text);

                return Results.Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error handling Slack event: " + ex);
                return Results.StatusCode(500);
            }
        });

        app.Run();
    }
}
