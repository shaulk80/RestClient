using NUnit.Framework;
using RestClient.UI.Services;

namespace RestClient.UI.Tests.Services;

[TestFixture]
public class EditorLanguageMapperTests
{
    [TestCase("application/json", EditorLanguage.Json)]
    [TestCase("application/json; charset=utf-8", EditorLanguage.Json)]
    [TestCase("application/problem+json", EditorLanguage.Json)]
    [TestCase("text/xml", EditorLanguage.Xml)]
    [TestCase("application/atom+xml", EditorLanguage.Xml)]
    [TestCase("text/plain", EditorLanguage.PlainText)]
    [TestCase("", EditorLanguage.PlainText)]
    [TestCase(null, EditorLanguage.PlainText)]
    public void MapChoosesLanguageFromContentType(string? contentType, EditorLanguage expected)
    {
        var language = EditorLanguageMapper.Map(contentType, 10);

        Assert.That(language, Is.EqualTo(expected));
    }

    [TestCase(EditorLanguageMapper.LargeBodyLimitBytes, EditorLanguage.Json)]
    [TestCase(EditorLanguageMapper.LargeBodyLimitBytes + 1, EditorLanguage.PlainText)]
    public void MapUsesPlainTextAboveLargeBodyLimit(int byteCount, EditorLanguage expected)
    {
        var language = EditorLanguageMapper.Map("application/json", byteCount);

        Assert.That(language, Is.EqualTo(expected));
    }
}
