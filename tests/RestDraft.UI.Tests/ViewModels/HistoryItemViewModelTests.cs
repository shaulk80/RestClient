using NUnit.Framework;
using RestDraft.Domain.Collections;
using RestDraft.Domain.History;
using RestDraft.UI.ViewModels;

namespace RestDraft.UI.Tests.ViewModels;

[TestFixture]
public class HistoryItemViewModelTests
{
    [TestCase(200, null, RequestMethod.Get, "GET 200")]
    [TestCase(null, "The request timed out.", RequestMethod.Post, "POST The request timed out.")]
    [TestCase(null, null, RequestMethod.Delete, "DELETE Failed")]
    public void TitleShowsMethodAndStatusOrError(int? statusCode, string? errorMessage, RequestMethod method, string expected)
    {
        var entry = CreateEntry(method, statusCode, errorMessage);

        var viewModel = new HistoryItemViewModel(entry);

        Assert.That(viewModel.Title, Is.EqualTo(expected));
    }

    private static HistoryEntry CreateEntry(RequestMethod method, int? statusCode, string? errorMessage)
    {
        var request = new RequestDefinition { Id = Guid.NewGuid(), Method = method, Url = "https://example.com" };
        return new HistoryEntry(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero),
            method,
            "https://example.com",
            request,
            statusCode,
            3,
            errorMessage,
            null);
    }
}
