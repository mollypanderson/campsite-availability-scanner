using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// 🔹 Twilio credentials (replace with yours from https://console.twilio.com/)
var accountSid = Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID");
var authToken  = Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN");

// ✅ 1. Endpoint to send a WhatsApp message
app.MapGet("/send", () =>
{
    TwilioClient.Init(accountSid, authToken);

    var message = MessageResource.Create(
        from: new PhoneNumber("whatsapp:+14155238886"), // Twilio sandbox number
        to: new PhoneNumber("whatsapp:+14257366255"),    // Your verified WhatsApp #
        body: "Hello from .NET 7 + Twilio WhatsApp!"
    );

    return Results.Ok(new { message.Sid, message.Status });
});

// ✅ 2. Webhook to receive incoming WhatsApp messages
app.MapPost("/whatsapp", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    string from = form["From"];
    string body = form["Body"];

    Console.WriteLine($"📩 Received WhatsApp message from {from}: {body}");

    // 🔹 Auto-reply (optional)
    TwilioClient.Init(accountSid, authToken);
    MessageResource.Create(
        from: new PhoneNumber("whatsapp:+14155238886"),
        to: new PhoneNumber(from),
        body: $"Thanks! You said: {body}"
    );

    return Results.Ok();
});

app.Run();
