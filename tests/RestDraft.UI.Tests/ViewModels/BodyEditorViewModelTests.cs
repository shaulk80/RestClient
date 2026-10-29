using FakeItEasy;
using NUnit.Framework;
using RestDraft.Domain.Bodies;
using RestDraft.Domain.Collections;
using RestDraft.UI.Services;
using RestDraft.UI.ViewModels;

namespace RestDraft.UI.Tests.ViewModels;

[TestFixture]
public class BodyEditorViewModelTests
{
    [TestCaseSource(nameof(StoredBodyTypes))]
    public void SelectsTypeOfStoredBody(BodyDefinition body, BodyType expected)
    {
        var viewModel = CreateEditor(body);

        Assert.That(viewModel.Type, Is.EqualTo(expected));
    }

    [Test]
    public void BuildReturnsStoredRawBody()
    {
        var viewModel = CreateEditor(new RawBody("application/json", "{}"));

        Assert.That(viewModel.Build(), Is.EqualTo(new RawBody("application/json", "{}")));
    }

    [Test]
    public void BuildReturnsStoredFormFields()
    {
        var form = new FormUrlEncodedBody([new KeyValueEntry("user", "me", true), new KeyValueEntry("skip", "1", false)]);
        var viewModel = CreateEditor(form);

        var built = viewModel.Build() as FormUrlEncodedBody;

        Assert.That(built?.Fields, Is.EqualTo(form.Fields));
    }

    [Test]
    public void BuildReturnsStoredMultipartParts()
    {
        var multipart = new MultipartBody([new MultipartPart("title", "x", null), new MultipartPart("file", null, "C:\a.txt")]);
        var viewModel = CreateEditor(multipart);

        var built = viewModel.Build() as MultipartBody;

        Assert.That(built?.Parts, Is.EqualTo(multipart.Parts));
    }

    [Test]
    public void BuildTrimsContentTypeAndReturnsNoBodyForEmptyText()
    {
        var viewModel = CreateEditor(new NoBody());
        viewModel.SelectedType = ChoiceOf(viewModel, BodyType.Raw);
        viewModel.ContentType = " text/plain ";

        Assert.That(viewModel.Build(), Is.EqualTo(new NoBody()));

        viewModel.Text = "x";

        Assert.That(viewModel.Build(), Is.EqualTo(new RawBody("text/plain", "x")));
    }

    [Test]
    public void SwitchingBackToRawKeepsRawText()
    {
        var viewModel = CreateEditor(new RawBody("text/plain", "kept"));

        viewModel.SelectedType = ChoiceOf(viewModel, BodyType.FormUrlEncoded);
        var form = viewModel.Build();
        viewModel.SelectedType = ChoiceOf(viewModel, BodyType.Raw);

        Assert.That(form, Is.InstanceOf<FormUrlEncodedBody>());
        Assert.That(viewModel.Build(), Is.EqualTo(new RawBody("text/plain", "kept")));
    }

    [TestCase("application/json", true)]
    [TestCase("application/xml", true)]
    [TestCase("text/plain", false)]
    [TestCase("", false)]
    public void CanFormatOnlyForRawJsonOrXml(string contentType, bool expected)
    {
        var viewModel = CreateEditor(new RawBody(contentType, "{}"));

        Assert.That(viewModel.CanFormat, Is.EqualTo(expected));
    }

    [Test]
    public void CanFormatIsFalseForFormBodies()
    {
        var viewModel = CreateEditor(new RawBody("application/json", "{}"));

        viewModel.SelectedType = ChoiceOf(viewModel, BodyType.FormUrlEncoded);

        Assert.That(viewModel.CanFormat, Is.False);
    }

    [Test]
    public void FormatReplacesTextWithFormattedJson()
    {
        var viewModel = CreateEditor(new RawBody("application/json", "{\"a\":1}"));

        viewModel.FormatCommand.Execute(null);

        Assert.That(viewModel.Text.Replace("\r\n", "\n"), Is.EqualTo("{\n  \"a\": 1\n}"));
        Assert.That(viewModel.Message, Is.Empty);
    }

    [Test]
    public void FormatShowsParserErrorAndKeepsMalformedText()
    {
        var viewModel = CreateEditor(new RawBody("application/json", "{\"a\":"));

        viewModel.FormatCommand.Execute(null);

        Assert.That(viewModel.Text, Is.EqualTo("{\"a\":"));
        Assert.That(viewModel.Message, Is.Not.Empty);
    }

    [Test]
    public void FormatWorksOnBodiesAboveLargeBodyLimit()
    {
        var numbers = string.Join(",", Enumerable.Repeat("1", 600000));
        var viewModel = CreateEditor(new RawBody("application/json", "[" + numbers + "]"));

        Assert.That(viewModel.IsLargeBody, Is.True);
        Assert.That(viewModel.CanFormat, Is.True);

        viewModel.FormatCommand.Execute(null);

        Assert.That(viewModel.Message, Is.Empty);
        Assert.That(viewModel.Text, Does.Contain(Environment.NewLine));
    }

    [TestCase(1048576, EditorLanguage.Json, false)]
    [TestCase(1048577, EditorLanguage.PlainText, true)]
    public void LargeBodyLimitSwitchesToPlainText(int byteCount, EditorLanguage expectedLanguage, bool expectedLarge)
    {
        var viewModel = CreateEditor(new RawBody("application/json", string.Empty));

        viewModel.Text = new string('a', byteCount);

        Assert.That(viewModel.Language, Is.EqualTo(expectedLanguage));
        Assert.That(viewModel.IsLargeBody, Is.EqualTo(expectedLarge));
    }

    [Test]
    public void EditingTextRaisesEdited()
    {
        var viewModel = CreateEditor(new NoBody());
        var raised = 0;
        viewModel.Edited += (_, _) => raised++;

        viewModel.Text = "x";
        viewModel.Text = "x";

        Assert.That(raised, Is.EqualTo(1));
    }

    [Test]
    public void AddingFormFieldRaisesEdited()
    {
        var viewModel = CreateEditor(new FormUrlEncodedBody([]));
        var raised = 0;
        viewModel.Edited += (_, _) => raised++;

        viewModel.Form.AddRowCommand.Execute(null);

        Assert.That(raised, Is.EqualTo(1));
    }

    [TestCaseSource(nameof(JsonBodyPreparations))]
    public void PrepareJsonBodyChoosesRawJsonOnlyWhenNothingIsChosen(BodyDefinition body, BodyType expectedType, string expectedContentType)
    {
        var viewModel = CreateEditor(body);

        viewModel.PrepareJsonBody();

        Assert.That(viewModel.Type, Is.EqualTo(expectedType));
        Assert.That(viewModel.ContentType, Is.EqualTo(expectedContentType));
    }

    public static IEnumerable<TestCaseData> JsonBodyPreparations()
    {
        yield return new TestCaseData(new NoBody(), BodyType.Raw, "application/json");
        yield return new TestCaseData(new RawBody(string.Empty, "x"), BodyType.Raw, "application/json");
        yield return new TestCaseData(new RawBody("text/plain", "x"), BodyType.Raw, "text/plain");
        yield return new TestCaseData(new FormUrlEncodedBody([]), BodyType.FormUrlEncoded, string.Empty);
    }

    public static IEnumerable<TestCaseData> StoredBodyTypes()
    {
        return
        [
            new TestCaseData(new NoBody(), BodyType.None),
            new TestCaseData(new RawBody("text/plain", "x"), BodyType.Raw),
            new TestCaseData(new FormUrlEncodedBody([]), BodyType.FormUrlEncoded),
            new TestCaseData(new MultipartBody([]), BodyType.Multipart),
        ];
    }

    private static BodyEditorViewModel CreateEditor(BodyDefinition body)
    {
        return new BodyEditorViewModel(RequestTabServicesFactory.CreateFormatting(), A.Fake<IFilePickerService>(), body);
    }

    private static BodyTypeChoice ChoiceOf(BodyEditorViewModel viewModel, BodyType type)
    {
        return viewModel.Types.Single(choice => choice.Type == type);
    }
}
