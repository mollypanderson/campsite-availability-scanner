using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

public static class SlackRequestVerifier
{
    /// <summary>
    /// Verifies the Slack request signature asynchronously.
    /// </summary>
    /// <param name="request">The incoming HttpRequest from Slack.</param>
    /// <param name="signingSecret">Your Slack signing secret.</param>
    /// <returns>True if the request is valid, false otherwise.</returns>
    public static async Task<bool> VerifyRequestAsync(HttpRequest request, string signingSecret)
    {
        if (!request.Headers.TryGetValue("X-Slack-Request-Timestamp", out var timestampHeader) ||
            !request.Headers.TryGetValue("X-Slack-Signature", out var slackSignatureHeader))
        {
            return false;
        }

        string timestamp = timestampHeader.First();
        string slackSignature = slackSignatureHeader.First();

        // Prevent replay attacks (optional but recommended)
        if (!long.TryParse(timestamp, out var ts) || Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts) > 60 * 5)
        {
            return false;
        }

        // Read body asynchronously
        string body;
        request.EnableBuffering(); // Allow multiple reads if necessary
        using (var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync();
            request.Body.Position = 0; // Reset stream for downstream processing
        }

        // Compute HMAC SHA256
        var sigBasestring = $"v0:{timestamp}:{body}";
        using var hasher = new HMACSHA256(Encoding.UTF8.GetBytes(signingSecret));
        var hash = hasher.ComputeHash(Encoding.UTF8.GetBytes(sigBasestring));
        var computedSignature = "v0=" + BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

        // Compare
        return SlowEquals(computedSignature, slackSignature);
    }

    // Constant-time comparison to prevent timing attacks
    private static bool SlowEquals(string a, string b)
    {
        uint diff = (uint)a.Length ^ (uint)b.Length;
        for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            diff |= (uint)(a[i] ^ b[i]);
        }
        return diff == 0;
    }
}
