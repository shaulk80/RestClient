using RestDraft.Domain.History;

namespace RestDraft.Application.History;

public sealed record RetentionPolicy(int MaxEntries, TimeSpan MaxAge)
{
    public static RetentionPolicy Default { get; } = new(1000, TimeSpan.FromDays(30));

    // Entries older than MaxAge go first. Of the rest, only the newest MaxEntries are kept.
    public IReadOnlyList<HistoryEntry> SelectExpired(IReadOnlyList<HistoryEntry> entries, DateTimeOffset now)
    {
        var cutoff = now - MaxAge;
        var newestFirst = entries.OrderByDescending(entry => entry.SentAt).ToList();
        var tooOld = newestFirst.Where(entry => entry.SentAt < cutoff);
        var overflow = newestFirst.Where(entry => entry.SentAt >= cutoff).Skip(MaxEntries);
        return tooOld.Concat(overflow).ToList();
    }
}
