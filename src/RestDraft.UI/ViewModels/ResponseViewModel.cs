using System.Text;
using RestDraft.Domain.Collections;
using RestDraft.Domain.Responses;
using RestDraft.UI.Services;

namespace RestDraft.UI.ViewModels;

public sealed class ResponseViewModel : ViewModelBase
{
    private const string ContentTypeHeader = "Content-Type";

    private readonly BodyFormatting _formatting;
    private string _statusText = string.Empty;
    private string _timingText = string.Empty;
    private string _sizeText = string.Empty;
    private string _bodyText = string.Empty;
    private string _bodyNote = string.Empty;
    private string _errorMessage = string.Empty;
    private IReadOnlyList<KeyValueEntry> _headers = [];
    private EditorLanguage _language = EditorLanguage.PlainText;
    private string _rawBody = string.Empty;
    private string? _prettyBody;
    private bool _isPrettyView;

    public ResponseViewModel(BodyFormatting formatting)
    {
        _formatting = formatting;
    }

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

    // Pretty and raw are both shown with the same highlighting. Pretty is the default when the body can be formatted.
    public string BodyText
    {
        get => _bodyText;
        private set => SetProperty(ref _bodyText, value);
    }

    public EditorLanguage Language
    {
        get => _language;
        private set => SetProperty(ref _language, value);
    }

    public bool CanPrettyPrint => _prettyBody is not null;

    public bool IsPrettyView
    {
        get => _isPrettyView;
        set
        {
            if (SetProperty(ref _isPrettyView, value))
            {
                RaisePropertyChanged(nameof(IsRawView));
                UpdateBodyText();
            }
        }
    }

    public bool IsRawView
    {
        get => !IsPrettyView;
        set => IsPrettyView = !value;
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
        _rawBody = snapshot.BodyPreview;
        Language = EditorLanguageMapper.Map(FindContentType(snapshot.Headers), Encoding.UTF8.GetByteCount(_rawBody));
        var pretty = PrettyPrint(Language, _rawBody);
        _prettyBody = pretty.Value;
        RaisePropertyChanged(nameof(CanPrettyPrint));
        IsPrettyView = CanPrettyPrint;
        BodyNote = JoinNotes(FormatBodyNote(snapshot), pretty.Note);
        UpdateBodyText();
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
        _rawBody = string.Empty;
        _prettyBody = null;
        RaisePropertyChanged(nameof(CanPrettyPrint));
        Language = EditorLanguage.PlainText;
        BodyNote = string.Empty;
        ErrorMessage = string.Empty;
        UpdateBodyText();
    }

    private void UpdateBodyText()
    {
        BodyText = IsPrettyView && _prettyBody is not null ? _prettyBody : _rawBody;
    }

    private PrettyResult PrettyPrint(EditorLanguage language, string body)
    {
        if (!BodyFormatting.CanFormat(language))
        {
            return new PrettyResult(null, null);
        }

        var result = _formatting.Format(language, body);
        return result.IsSuccess
            ? new PrettyResult(result.Value, null)
            : new PrettyResult(null, $"Pretty print failed: {ErrorMessageFormatter.Format(result.Error!)}");
    }

    private static string? FindContentType(IReadOnlyList<KeyValueEntry> headers)
    {
        return headers.FirstOrDefault(header => string.Equals(header.Key, ContentTypeHeader, StringComparison.OrdinalIgnoreCase))?.Value;
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

    private static string JoinNotes(string first, string? second)
    {
        return string.IsNullOrEmpty(second) ? first : $"{first} {second}".Trim();
    }

    private static string Milliseconds(double value)
    {
        return $"{Math.Round(value)} ms";
    }

    private sealed record PrettyResult(string? Value, string? Note);
}
