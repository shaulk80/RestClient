namespace RestDraft.UI.Services;

// Tells the user why the app could not start. The Windows build is a GUI app with no console, so stderr
// alone would show nothing.
public sealed class StartupErrorReporter
{
    public const string Title = "RestDraft";

    private readonly TextWriter _errorOutput;
    private readonly Action<string, string> _showDialog;

    public StartupErrorReporter(TextWriter errorOutput, Action<string, string> showDialog)
    {
        _errorOutput = errorOutput;
        _showDialog = showDialog;
    }

    public static StartupErrorReporter CreateDefault()
    {
        return new StartupErrorReporter(Console.Error, NativeMessageBox.ShowError);
    }

    public void Report(string? message, string? details)
    {
        var text = Format(message, details);
        _errorOutput.WriteLine(text);
        _showDialog(Title, text);
    }

    public static string Format(string? message, string? details)
    {
        var summary = string.IsNullOrWhiteSpace(message) ? "An unknown error stopped the app from starting." : message.Trim();
        if (string.IsNullOrWhiteSpace(details))
        {
            return summary;
        }

        return $"{summary}{Environment.NewLine}{Environment.NewLine}{details.Trim()}";
    }
}
