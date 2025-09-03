public static class Utils
{
    public static bool IsCommaDelimitedNumbers(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;

        return input
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .All(s => int.TryParse(s.Trim(), out _)); // returns true only if all parts are integers
    }
}