using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using CampsiteAvailabilityScanner.Services;

class Program
{
    static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddHostedService<ScheduledTaskService>();
        builder.WebHost.UseUrls("http://localhost:5167"); // <-- force HTTP
        var app = builder.Build();

        // Configure Twilio credentials
        DotNetEnv.Env.Load();
        string accountSid = Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID")!;
        string authToken = Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN")!;
        string sandboxNumber = "whatsapp:+14155238886";

        // Instantiate the bot service
        var botService = new WhatsAppBotService(accountSid, authToken, sandboxNumber);

        // Twilio webhook endpoint
        app.MapPost("/whatsapp", async (HttpRequest request) =>
        {
            var form = await request.ReadFormAsync();
            string from = form["From"];
            string body = form["Body"];

            botService.HandleIncomingMessage(from, body);

            return Results.Ok();
        });

        app.Run();

    }
}