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
        DotNetEnv.Env.Load(); // Load from project root

        var builder = WebApplication.CreateBuilder(args);

        var port = Utils.ReadSecret("PORT") ?? "8080";
        //  builder.WebHost.UseUrls($"http://localhost:{port}"); // HTTP only
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

        builder.Services.AddHostedService<ScheduledTaskService>();

        var app = builder.Build();

        // Load environment variables
        string env = Utils.ReadSecret("ENV") ?? "development";
        string slackBotToken = Utils.ReadSecret("SLACK_BOT_TOKEN")!;
        string slackSigningSecret = Utils.ReadSecret("SLACK_SIGNING_SECRET")!;
        string channelId = Utils.ReadSecret("CHANNEL_ID")!;

        var slackClient = new SlackClient(slackBotToken);

        Console.WriteLine($"Running in {env} mode");

        string mongoUri = Utils.ReadSecret("MONGO_URI")!;
        var mongoService = new MongoService(
            mongoUri,
            "campsite-tracking-db",
            "user-campsite-tracking"
        );

        var slackBotService = new SlackBotService(slackClient, channelId, mongoService);

        app.MapPost("/slack/events", async (HttpRequest request) =>
        {
            if (!await SlackRequestVerifier.VerifyRequestAsync(request, slackSigningSecret))
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
                await slackBotService.HandleIncomingMessageAsync(userId, channelId, text);

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
