using NUnit.Framework;
using RestClient.Domain.Bodies;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;
using RestClient.Domain.Validation;

namespace RestClient.Domain.Tests.Validation;

[TestFixture]
public class RequestValidatorTests
{
    [Test]
    public void ValidateAcceptsWellFormedDefinition()
    {
        var definition = CreateDefinition() with
        {
            QueryParams = [new KeyValueEntry("q", "1", true)],
            Headers = [new KeyValueEntry("Accept", "application/json", true)],
            Body = new MultipartBody([new MultipartPart("file", null, "C:\\data\\input.txt")]),
        };

        var result = RequestValidator.Validate(definition);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public void ValidateIgnoresDisabledEntriesWithEmptyNames()
    {
        var definition = CreateDefinition() with
        {
            QueryParams = [new KeyValueEntry("", "1", false)],
            Headers = [new KeyValueEntry(" ", "x", false)],
        };

        var result = RequestValidator.Validate(definition);

        Assert.That(result.IsSuccess, Is.True);
    }

    [TestCaseSource(nameof(InvalidDefinitions))]
    public void ValidateRejectsInvalidDefinition(RequestDefinition definition)
    {
        var result = RequestValidator.Validate(definition);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
    }

    [TestCase("http://example.com", true)]
    [TestCase("https://example.com/a?b=c", true)]
    [TestCase("example.com", false)]
    [TestCase("/relative/path", false)]
    [TestCase("ftp://example.com", false)]
    [TestCase("", false)]
    public void ValidateUrlAcceptsOnlyAbsoluteHttpUrls(string url, bool expected)
    {
        var result = RequestValidator.ValidateUrl(url);

        Assert.That(result.IsSuccess, Is.EqualTo(expected));
    }

    public static IEnumerable<RequestDefinition> InvalidDefinitions()
    {
        return
        [
            CreateDefinition() with { Method = (RequestMethod)99 },
            CreateDefinition() with { QueryParams = [new KeyValueEntry(" ", "1", true)] },
            CreateDefinition() with { Headers = [new KeyValueEntry("", "x", true)] },
            CreateDefinition() with { Body = new MultipartBody([new MultipartPart("both", "value", "C:\\a.txt")]) },
            CreateDefinition() with { Body = new MultipartBody([new MultipartPart("neither", null, null)]) },
        ];
    }

    private static RequestDefinition CreateDefinition()
    {
        return new RequestDefinition
        {
            Id = Guid.Empty,
            Url = "https://example.com",
        };
    }
}
