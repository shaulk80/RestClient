using NUnit.Framework;
using RestDraft.Application.History;
using RestDraft.Domain.Auth;
using RestDraft.Domain.Collections;

namespace RestDraft.Application.Tests.History;

[TestFixture]
public class HistoryUrlTests
{
    [Test]
    public void UrlWithoutQueryParamsIsReturnedAsTyped()
    {
        var definition = CreateDefinition("https://{{host}}/orders", []);

        Assert.That(HistoryUrl.For(definition), Is.EqualTo("https://{{host}}/orders"));
    }

    [Test]
    public void EnabledQueryParamsAreAppendedWithTheirPlaceholdersUnresolved()
    {
        var definition = CreateDefinition(
            "https://{{host}}/orders",
            [
                new KeyValueEntry("token", "{{apiToken}}", true),
                new KeyValueEntry("debug", "1", false),
                new KeyValueEntry("page", "2", true),
            ]);

        Assert.That(HistoryUrl.For(definition), Is.EqualTo("https://{{host}}/orders?token={{apiToken}}&page=2"));
    }

    [Test]
    public void OnlyDisabledQueryParamsLeaveTheUrlAlone()
    {
        var definition = CreateDefinition("https://example.com", [new KeyValueEntry("debug", "1", false)]);

        Assert.That(HistoryUrl.For(definition), Is.EqualTo("https://example.com"));
    }

    [Test]
    public void ApiKeyInTheQueryIsNotPartOfTheHistoryUrl()
    {
        var definition = CreateDefinition("https://example.com", []) with
        {
            Auth = new ApiKeyAuth("api_key", "{{secretKey}}", ApiKeyLocation.Query),
        };

        Assert.That(HistoryUrl.For(definition), Is.EqualTo("https://example.com"));
    }

    private static RequestDefinition CreateDefinition(string url, IReadOnlyList<KeyValueEntry> queryParams)
    {
        return new RequestDefinition { Id = Guid.NewGuid(), Name = "Test", Url = url, QueryParams = queryParams };
    }
}
