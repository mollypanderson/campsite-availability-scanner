using System.Security.Cryptography;
using System.Text;

public static class SlackRequestVerifier
{
    public static bool VerifyRequest(HttpRequest request, string signingSecret)
    {
        // Get timestamp and signature from headers
        if (!request.Headers.TryGetValue("X-Slack-Request-Timestamp", out var timestampHeader) ||
            !request.Headers.TryGetValue("X-Slack-Signature", out var signatureHeader))
        {
            return false;
        }

        string timestamp = timestampHeader.ToString();
        string slackSignature = signatureHeader.ToString();

        // Prevent replay attacks: reject if timestamp is more than 5 minutes old
        long ts = long.Parse(timestamp);
        var diff = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts;
        if (Math.Abs(diff) > 60 * 5) return false;

        // Read request body
        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        string body = reader.ReadToEnd();
        request.Body.Position = 0; // reset stream for downstream processing

        // Compute HMAC SHA256
        string baseString = $"v0:{timestamp}:{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
        string computedSignature = "v0=" + BitConverter.ToString(hash).Replace("-", "").ToLower();

        return computedSignature == slackSignature;
    }
}
