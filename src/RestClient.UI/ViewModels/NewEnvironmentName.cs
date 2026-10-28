namespace RestClient.UI.ViewModels;

// The name a new environment gets before the user types its own: "New environment", then "New environment 2", ...
public static class NewEnvironmentName
{
    private const string BaseName = "New environment";

    public static string NextAvailable(IEnumerable<string> existing)
    {
        var taken = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = BaseName;
        for (var suffix = 2; taken.Contains(candidate); suffix++)
        {
            candidate = $"{BaseName} {suffix}";
        }

        return candidate;
    }
}
