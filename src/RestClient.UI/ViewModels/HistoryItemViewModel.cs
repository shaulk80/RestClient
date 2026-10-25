using System.Globalization;
using RestClient.Domain.Collections;
using RestClient.Domain.History;

namespace RestClient.UI.ViewModels;

public sealed class HistoryItemViewModel
{
    public HistoryItemViewModel(HistoryEntry entry)
    {
        Id = entry.Id;
        Method = entry.Method;
        Url = entry.ResolvedUrl;
        Title = $"{entry.Method.ToString().ToUpperInvariant()} {Outcome(entry)}";
        Details = $"{entry.SentAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}  {Math.Round(entry.DurationMs).ToString(CultureInfo.InvariantCulture)} ms";
    }

    public Guid Id { get; }

    public RequestMethod Method { get; }

    public string Url { get; }

    public string Title { get; }

    public string Details { get; }

    private static string Outcome(HistoryEntry entry)
    {
        return entry.StatusCode is { } statusCode
            ? statusCode.ToString(CultureInfo.InvariantCulture)
            : entry.ErrorMessage ?? "Failed";
    }
}
