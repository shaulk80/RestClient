using RestClient.Domain.Collections;

namespace RestClient.Application.Requests;

public static class KeyValueEncoder
{
    public static string Encode(IEnumerable<KeyValueEntry> entries)
    {
        return string.Join("&", entries.Select(entry => $"{Uri.EscapeDataString(entry.Key)}={Uri.EscapeDataString(entry.Value)}"));
    }
}
