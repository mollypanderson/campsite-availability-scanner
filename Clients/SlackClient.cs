using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class SlackClient : ISlackClient
{
    private readonly HttpClient _httpClient;

    public SlackClient(string token)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public async Task SendMessageToChannelAsync(string channelId, string text)
    {
        var payload = new
        {
            channel = channelId,
            text = text
        };

        var response = await _httpClient.PostAsJsonAsync(
            "https://slack.com/api/chat.postMessage", payload);

        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        // Optional: check Slack "ok": true
        Console.WriteLine($"Sent to Slack: {responseBody}");
        Console.WriteLine($"Slack API response: {responseBody}");
    }
    
        //     private async Task SendMessageToChannelAsync
        // (ISlackClient slackClient, string text)
        // {
        //     using var http = new HttpClient();
        //     http.DefaultRequestHeaders.Authorization =
        //         new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _slackBotToken);

        //     var payload = new
        //     {
        //         channel = channelId,
        //         text = text
        //     };
        //     var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

        //     // Fire-and-forget is safer if you don't need response immediately
        //     var response = await http.PostAsync("https://slack.com/api/chat.postMessage", content);
        //     var body = await response.Content.ReadAsStringAsync();
        //     Console.WriteLine($"Sent to Slack: {body}");
        // }
}