using NUnit.Framework;
using RestClient.Domain.Collections;
using RestClient.Domain.Responses;
using RestClient.UI.Services;
using RestClient.UI.ViewModels;

namespace RestClient.UI.Tests.ViewModels;

[TestFixture]
public class ResponseViewModelTests
{
    private const string Json = "{\"a\":1}";

    [Test]
    public void ShowPrettyPrintsJsonByDefault()
    {
        var viewModel = new ResponseViewModel(RequestTabServicesFactory.CreateFormatting());

        viewModel.Show(CreateSnapshot("application/json", Json));

        Assert.That(viewModel.Language, Is.EqualTo(EditorLanguage.Json));
        Assert.That(viewModel.CanPrettyPrint, Is.True);
        Assert.That(viewModel.IsPrettyView, Is.True);
        Assert.That(viewModel.BodyText.Replace("\r\n", "\n"), Is.EqualTo("{\n  \"a\": 1\n}"));
    }

    [Test]
    public void RawViewShowsBodyAsReceived()
    {
        var viewModel = new ResponseViewModel(RequestTabServicesFactory.CreateFormatting());
        viewModel.Show(CreateSnapshot("application/json", Json));

        viewModel.IsRawView = true;

        Assert.That(viewModel.BodyText, Is.EqualTo(Json));
    }

    [Test]
    public void InvalidJsonFallsBackToRawWithNote()
    {
        var viewModel = new ResponseViewModel(RequestTabServicesFactory.CreateFormatting());

        viewModel.Show(CreateSnapshot("application/json", "{"));

        Assert.That(viewModel.CanPrettyPrint, Is.False);
        Assert.That(viewModel.IsRawView, Is.True);
        Assert.That(viewModel.BodyText, Is.EqualTo("{"));
        Assert.That(viewModel.BodyNote, Does.StartWith("Pretty print failed:"));
    }

    [Test]
    public void PlainTextIsShownAsReceived()
    {
        var viewModel = new ResponseViewModel(RequestTabServicesFactory.CreateFormatting());

        viewModel.Show(CreateSnapshot("text/plain", "hello"));

        Assert.That(viewModel.Language, Is.EqualTo(EditorLanguage.PlainText));
        Assert.That(viewModel.CanPrettyPrint, Is.False);
        Assert.That(viewModel.BodyText, Is.EqualTo("hello"));
    }

    [Test]
    public void ClearRemovesBodyAndLanguage()
    {
        var viewModel = new ResponseViewModel(RequestTabServicesFactory.CreateFormatting());
        viewModel.Show(CreateSnapshot("application/json", Json));

        viewModel.Clear();

        Assert.That(viewModel.BodyText, Is.Empty);
        Assert.That(viewModel.Language, Is.EqualTo(EditorLanguage.PlainText));
        Assert.That(viewModel.CanPrettyPrint, Is.False);
    }

    private static ResponseSnapshot CreateSnapshot(string contentType, string body)
    {
        KeyValueEntry[] headers = [new KeyValueEntry("Content-Type", contentType, true)];
        return new ResponseSnapshot(200, "OK", headers, body, false, body.Length, null, new ResponseTiming(1, 2, 3));
    }
}
