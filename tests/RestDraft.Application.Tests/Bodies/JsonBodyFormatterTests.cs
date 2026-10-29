using NUnit.Framework;
using RestDraft.Application.Bodies;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Tests.Bodies;

[TestFixture]
public class JsonBodyFormatterTests
{
    private readonly JsonBodyFormatter _formatter = new();

    [TestCase("{\"a\":1,\"b\":[true,null]}", "{\n  \"a\": 1,\n  \"b\": [\n    true,\n    null\n  ]\n}")]
    [TestCase("[1,2]", "[\n  1,\n  2\n]")]
    [TestCase("{\"n\":{\"m\":\"é\"}}", "{\n  \"n\": {\n    \"m\": \"é\"\n  }\n}")]
    public void FormatIndentsValidJson(string input, string expected)
    {
        var result = _formatter.Format(input);

        Assert.That(result.Value?.Replace("\r\n", "\n"), Is.EqualTo(expected));
    }

    [TestCase("{\"a\":")]
    [TestCase("[1,]")]
    [TestCase("{")]
    [TestCase("")]
    [TestCase("   ")]
    public void FormatFailsWithValidationErrorForInvalidInput(string input)
    {
        var result = _formatter.Format(input);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
    }
}
