using RestClient.Domain.Collections;
using RestClient.Domain.Responses;

namespace RestClient.UI.ViewModels;

public sealed class ResponseViewModel : ViewModelBase
{
    private string _statusText = string.Empty;
    private string _timingText = string.Empty;
    private string _sizeText = string.Empty;
    private string _bodyText = string.Empty;
    private string _bodyNote = string.Empty;
    private string _errorMessage = string.Empty;
    private IReadOnlyList<KeyValueEntry> _headers = [];

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string TimingText
    {
        get => _timingText;
        private set => SetProperty(ref _timingText, value);
    }

    public string SizeText
    {
        get => _sizeText;
        private set => SetProperty(ref _sizeText, value);
    }

    public IReadOnlyList<KeyValueEntry> Headers
    {
        get => _headers;
        private set => SetProperty(ref _headers, value);
    }

    public string BodyText
    {
        get => _bodyText;
        private set => SetProperty(ref _bodyText, value);
    }

    public string BodyNote
    {
        get => _bodyNote;
        private set => SetProperty(ref _bodyNote, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public void Show(ResponseSnapshot snapshot)
    {
        Clear();
        StatusText = $"{snapshot.StatusCode} {snapshot.ReasonPhrase}".TrimEnd();
        TimingText = FormatTiming(snapshot.Timing);
        SizeText = $"{snapshot.BodySizeBytes} bytes";
        Headers = snapshot.Headers;
        BodyText = snapshot.BodyPreview;
        BodyNote = FormatBodyNote(snapshot);
    }

    public void ShowError(string message)
    {
        Clear();
        ErrorMessage = message;
    }

    public void Clear()
    {
        StatusText = string.Empty;
        TimingText = string.Empty;
        SizeText = string.Empty;
        Headers = [];
        BodyText = string.Empty;
        BodyNote = string.Empty;
        ErrorMessage = string.Empty;
    }

    private static string FormatTiming(ResponseTiming timing)
    {
        var connect = timing.ConnectMs is { } connectMs ? Milliseconds(connectMs) : "n/a";
        return $"Total {Milliseconds(timing.TotalMs)}, first byte {Milliseconds(timing.TimeToFirstByteMs)}, connect {connect}";
    }

    private static string FormatBodyNote(ResponseSnapshot snapshot)
    {
        if (snapshot.BodyFilePath is { } path)
        {
            return $"Full body saved to {path}";
        }

        if (snapshot.IsPreviewTruncated)
        {
            return "Preview truncated.";
        }

        return string.Empty;
    }

    private static string Milliseconds(double value)
    {
        return $"{Math.Round(value)} ms";
    }
}
