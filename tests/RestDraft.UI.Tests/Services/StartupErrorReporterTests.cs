using NUnit.Framework;
using RestDraft.UI.Services;

namespace RestDraft.UI.Tests.Services;

[TestFixture]
public class StartupErrorReporterTests
{
    [TestCase("Cannot create the data folder.", "Access denied.", "Cannot create the data folder.\n\nAccess denied.")]
    [TestCase("  Cannot create the data folder. ", "  ", "Cannot create the data folder.")]
    [TestCase("Cannot create the data folder.", null, "Cannot create the data folder.")]
    [TestCase(null, "Access denied.", "An unknown error stopped the app from starting.\n\nAccess denied.")]
    [TestCase("", "", "An unknown error stopped the app from starting.")]
    public void FormatJoinsSummaryAndDetails(string? message, string? details, string expected)
    {
        var text = StartupErrorReporter.Format(message, details).Replace("\r\n", "\n");

        Assert.That(text, Is.EqualTo(expected));
    }

    [Test]
    public void ReportWritesToErrorOutputAndShowsTheDialog()
    {
        var output = new StringWriter();
        string? shownTitle = null;
        string? shownText = null;
        var reporter = new StartupErrorReporter(output, (title, text) =>
        {
            shownTitle = title;
            shownText = text;
        });

        reporter.Report("Migration failed.", "Script 0003 failed.");

        var expected = StartupErrorReporter.Format("Migration failed.", "Script 0003 failed.");
        Assert.That(output.ToString().TrimEnd(), Is.EqualTo(expected));
        Assert.That(shownTitle, Is.EqualTo(StartupErrorReporter.Title));
        Assert.That(shownText, Is.EqualTo(expected));
    }
}
