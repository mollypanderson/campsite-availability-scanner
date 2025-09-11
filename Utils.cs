public static class Utils
{
    public static bool IsCommaDelimitedNumbers(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;

        return input
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .All(s => int.TryParse(s.Trim(), out _)); // returns true only if all parts are integers
    }

    public static string ExtractUrlFromSlackMessage(string message)
    {
        message = message.Trim();

        if (message.StartsWith("<") && message.EndsWith(">"))
        {
            // Remove < and >
            message = message[1..^1];

            // Split on | and take the first part (the actual URL)
            var parts = message.Split('|');
            message = parts[0];
        }

        return message;
    }

    public static string ReadSecret(string name)
    {
        string secretPath = $"/run/secrets/{name}";
        if (File.Exists(secretPath))
            return File.ReadAllText(secretPath).Trim();
        else
            return Environment.GetEnvironmentVariable(name) ?? throw new Exception($"Secret {name} not found");
    }

}