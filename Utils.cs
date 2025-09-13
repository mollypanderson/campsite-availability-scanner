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

    public static string IncrementLabel(string label)
    {
        if (string.IsNullOrEmpty(label))
            return "A";

        // Convert to uppercase and to char array
        char[] chars = label.ToUpper().ToCharArray();
        int i = chars.Length - 1;

        while (i >= 0)
        {
            if (chars[i] < 'Z')
            {
                chars[i]++;
                return new string(chars);
            }
            else
            {
                chars[i] = 'A';
                i--;
            }
        }

        // If we carried past the first character, prepend 'A'
        return "A" + new string(chars);
    }

    public static PermitArea FilterPermitAreaBySelectedSites(PermitArea original, List<Site> selectedSites)
    {
        // Build new PermitArea
        var filteredPermitArea = new PermitArea
        {
            Name = original.Name,
            Id = original.Id,
            StartingAreas = original.StartingAreas
                .Select(sa =>
                {
                    // Keep only sites that are in selectedSites
                    var filteredSites = sa.Sites
                        .Where(s => selectedSites.Contains(s))
                        .ToList();

                    // Only keep startingAreas with at least one selected site
                    if (filteredSites.Count == 0) return null;

                    return new StartingArea
                    {
                        Name = sa.Name,
                        Sites = filteredSites
                    };
                })
                .Where(sa => sa != null) // remove nulls
                .ToList()!
        };
        return filteredPermitArea;
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