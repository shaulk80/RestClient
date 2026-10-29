using NUnit.Framework;
using RestDraft.Application.Bodies;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Tests.Bodies;

[TestFixture]
public class XmlBodyFormatterTests
{
    private readonly XmlBodyFormatter _formatter = new();

    [TestCase("<root><a>1</a></root>", "<root>\n  <a>1</a>\n</root>")]
    [TestCase("<root><a><b>x</b></a></root>", "<root>\n  <a>\n    <b>x</b>\n  </a>\n</root>")]
    [TestCase("<?xml version=\"1.0\" encoding=\"utf-8\"?><root/>", "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<root />")]
    public void FormatIndentsValidXml(string input, string expected)
    {
        var result = _formatter.Format(input);

        Assert.That(result.Value?.Replace("\r\n", "\n"), Is.EqualTo(expected));
    }

    [TestCase("<root>")]
    [TestCase("<a></b>")]
    [TestCase("")]
    [TestCase("   ")]
    public void FormatFailsWithValidationErrorForInvalidInput(string input)
    {
        var result = _formatter.Format(input);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
    }
}
