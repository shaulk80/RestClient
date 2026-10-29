using RestDraft.Domain.Collections;
using RestDraft.Domain.History;

namespace RestDraft.Application.Tests.History;

internal static class HistoryFixtures
{
    public static HistoryEntry Entry(DateTimeOffset sentAt, string? bodyFilePath = null)
    {
        var request = new RequestDefinition { Id = Guid.NewGuid(), Url = "https://example.com" };
        return new HistoryEntry(
            Guid.NewGuid(),
            sentAt,
            RequestMethod.Get,
            "https://example.com",
            request,
            200,
            1,
            null,
            bodyFilePath);
    }
}
