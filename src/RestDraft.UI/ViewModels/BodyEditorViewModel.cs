using System.Text;
using System.Windows.Input;
using RestDraft.Domain.Bodies;
using RestDraft.UI.Commands;
using RestDraft.UI.Services;

namespace RestDraft.UI.ViewModels;

public sealed class BodyEditorViewModel : ViewModelBase
{
    private static readonly IReadOnlyList<BodyTypeChoice> AllTypes =
    [
        new BodyTypeChoice(BodyType.None, "None"),
        new BodyTypeChoice(BodyType.Raw, "Raw"),
        new BodyTypeChoice(BodyType.FormUrlEncoded, "Form URL-encoded"),
        new BodyTypeChoice(BodyType.Multipart, "Multipart form-data"),
    ];

    private static readonly IReadOnlyList<string> AllContentTypes =
    [
        "application/json",
        "application/xml",
        "application/javascript",
        "application/octet-stream",
        "text/plain",
        "text/html",
        "text/xml",
        "text/csv",
    ];

    private readonly BodyFormatting _formatting;
    private readonly RelayCommand _formatCommand;
    private BodyTypeChoice _selectedType;
    private string _contentType;
    private string _text;
    private EditorLanguage _language = EditorLanguage.PlainText;
    private bool _isLargeBody;
    private string _message = string.Empty;

    public BodyEditorViewModel(BodyFormatting formatting, IFilePickerService files, BodyDefinition body)
    {
        _formatting = formatting;
        Form = new KeyValueEditorViewModel(body is FormUrlEncodedBody form ? form.Fields : []);
        Multipart = new MultipartEditorViewModel(files, body is MultipartBody multipart ? multipart.Parts : []);
        Form.Changed += OnNestedChanged;
        Multipart.Changed += OnNestedChanged;
        var raw = body as RawBody;
        _contentType = raw?.ContentType ?? string.Empty;
        _text = raw?.Text ?? string.Empty;
        _selectedType = Types.First(choice => choice.Type == TypeOf(body));
        _formatCommand = new RelayCommand(Format, () => CanFormat);
        RefreshState();
    }

    // Raised after any change a user makes, including a format. The tab uses it to mark itself dirty.
    public event EventHandler? Edited;

    // Shared by all tabs. The tab content view is reused when the selected tab changes, and a combo box whose
    // item list is replaced writes its old selection back to the new tab.
    public IReadOnlyList<BodyTypeChoice> Types { get; } = AllTypes;

    public BodyTypeChoice SelectedType
    {
        get => _selectedType;
        set => EditField(ref _selectedType, value, nameof(SelectedType));
    }

    // The suggestions of the content type box. The box is editable, so any other value can be typed or cleared.
    public IReadOnlyList<string> ContentTypes { get; } = AllContentTypes;

    public BodyType Type => _selectedType.Type;

    public bool IsNone => Type == BodyType.None;

    public bool IsRaw => Type == BodyType.Raw;

    public bool IsFormUrlEncoded => Type == BodyType.FormUrlEncoded;

    public bool IsMultipart => Type == BodyType.Multipart;

    public string ContentType
    {
        get => _contentType;
        set => EditField(ref _contentType, value, nameof(ContentType));
    }

    public string Text
    {
        get => _text;
        set => EditField(ref _text, value, nameof(Text));
    }

    public EditorLanguage Language
    {
        get => _language;
        private set => SetProperty(ref _language, value);
    }

    public bool IsLargeBody
    {
        get => _isLargeBody;
        private set => SetProperty(ref _isLargeBody, value);
    }

    // Formatting depends on the content type only, not on the size, so a large body can still be formatted.
    private EditorLanguage FormatLanguage => IsRaw ? EditorLanguageMapper.FromContentType(ContentType) : EditorLanguage.PlainText;

    public bool CanFormat => BodyFormatting.CanFormat(FormatLanguage);

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public ICommand FormatCommand => _formatCommand;

    public KeyValueEditorViewModel Form { get; }

    public MultipartEditorViewModel Multipart { get; }

    public BodyDefinition Build()
    {
        return Type switch
        {
            BodyType.Raw => BuildRaw(),
            BodyType.FormUrlEncoded => new FormUrlEncodedBody(Form.ToEntries()),
            BodyType.Multipart => new MultipartBody(Multipart.ToParts()),
            _ => new NoBody(),
        };
    }

    // For a method that usually carries a body: a request with no body becomes a JSON raw body. A body the user has
    // already chosen, and a content type already typed, are kept.
    public void PrepareJsonBody()
    {
        if (Type == BodyType.None)
        {
            SelectedType = Types.First(choice => choice.Type == BodyType.Raw);
        }

        if (Type == BodyType.Raw && ContentType.Length == 0)
        {
            ContentType = AllContentTypes[0];
        }
    }

    private static BodyType TypeOf(BodyDefinition body)
    {
        return body switch
        {
            RawBody => BodyType.Raw,
            FormUrlEncodedBody => BodyType.FormUrlEncoded,
            MultipartBody => BodyType.Multipart,
            _ => BodyType.None,
        };
    }

    private BodyDefinition BuildRaw()
    {
        return Text.Length == 0 ? new NoBody() : new RawBody(ContentType.Trim(), Text);
    }

    private void Format()
    {
        var result = _formatting.Format(FormatLanguage, Text);
        if (!result.IsSuccess)
        {
            Message = ErrorMessageFormatter.Format(result.Error!);
            return;
        }

        Text = result.Value!;
    }

    private void EditField<T>(ref T field, T value, string propertyName)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            Message = string.Empty;
            OnEdited();
        }
    }

    private void OnNestedChanged(object? sender, EventArgs e)
    {
        OnEdited();
    }

    private void OnEdited()
    {
        RefreshState();
        Edited?.Invoke(this, EventArgs.Empty);
    }

    // Language and size depend on the type, content type and text. Plain text is used above the size limit.
    private void RefreshState()
    {
        var byteCount = Type == BodyType.Raw ? Encoding.UTF8.GetByteCount(Text) : 0;
        Language = Type == BodyType.Raw
            ? EditorLanguageMapper.Map(ContentType, byteCount)
            : EditorLanguage.PlainText;
        IsLargeBody = byteCount > EditorLanguageMapper.LargeBodyLimitBytes;
        RaisePropertyChanged(nameof(Type));
        RaisePropertyChanged(nameof(IsNone));
        RaisePropertyChanged(nameof(IsRaw));
        RaisePropertyChanged(nameof(IsFormUrlEncoded));
        RaisePropertyChanged(nameof(IsMultipart));
        RaisePropertyChanged(nameof(CanFormat));
        _formatCommand.RaiseCanExecuteChanged();
    }
}
