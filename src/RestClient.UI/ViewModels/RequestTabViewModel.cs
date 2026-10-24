using System.Windows.Input;
using RestClient.Application.Abstractions;
using RestClient.Application.Collections;
using RestClient.Domain.Bodies;
using RestClient.Domain.Collections;
using RestClient.Domain.Responses;
using RestClient.Domain.Results;
using RestClient.UI.Commands;
using RestClient.UI.Services;

namespace RestClient.UI.ViewModels;

public sealed class RequestTabViewModel : ViewModelBase
{
    private static readonly IReadOnlyDictionary<string, string> NoVariables = new Dictionary<string, string>();

    private readonly IRequestSender _sender;
    private readonly IRequestService _requests;
    private readonly AsyncRelayCommand _sendCommand;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly RelayCommand _cancelCommand;
    private RequestDefinition _saved;
    private CancellationTokenSource? _cancellation;
    private string _name;
    private RequestMethod _method;
    private string _url;
    private string _contentType;
    private string _body;
    private bool _isSending;
    private bool _isDirty;
    private bool _isBodyEdited;
    private string _saveError = string.Empty;

    public RequestTabViewModel(IRequestSender sender, IRequestService requests, RequestDefinition definition)
    {
        _sender = sender;
        _requests = requests;
        _saved = definition;
        _name = definition.Name;
        _method = definition.Method;
        _url = definition.Url;
        var raw = definition.Body as RawBody;
        _contentType = raw?.ContentType ?? string.Empty;
        _body = raw?.Text ?? string.Empty;
        _sendCommand = new AsyncRelayCommand(SendAsync, () => !IsSending);
        _saveCommand = new AsyncRelayCommand(SaveAsync, () => true);
        _cancelCommand = new RelayCommand(Cancel, () => IsSending);
    }

    public Guid RequestId => _saved.Id;

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                RaisePropertyChanged(nameof(Header));
            }
        }
    }

    public string Header => IsDirty ? $"{Name} *" : Name;

    public IReadOnlyList<RequestMethod> Methods { get; } = Enum.GetValues<RequestMethod>();

    public RequestMethod Method
    {
        get => _method;
        set => EditField(ref _method, value, nameof(Method));
    }

    public string Url
    {
        get => _url;
        set => EditField(ref _url, value, nameof(Url));
    }

    public string ContentType
    {
        get => _contentType;
        set => EditBodyField(ref _contentType, value, nameof(ContentType));
    }

    public string Body
    {
        get => _body;
        set => EditBodyField(ref _body, value, nameof(Body));
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetProperty(ref _isDirty, value))
            {
                RaisePropertyChanged(nameof(Header));
            }
        }
    }

    public bool IsSending
    {
        get => _isSending;
        private set
        {
            if (SetProperty(ref _isSending, value))
            {
                _sendCommand.RaiseCanExecuteChanged();
                _cancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SaveError
    {
        get => _saveError;
        private set => SetProperty(ref _saveError, value);
    }

    public ResponseViewModel Response { get; } = new();

    public ICommand SendCommand => _sendCommand;

    public ICommand SaveCommand => _saveCommand;

    public ICommand CancelCommand => _cancelCommand;

    public async Task SendAsync()
    {
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        IsSending = true;
        Response.Clear();
        try
        {
            var result = await _sender.SendAsync(ApplyEdits(_saved), NoVariables, cancellation.Token);
            ShowResult(result);
        }
        finally
        {
            _cancellation = null;
            IsSending = false;
        }
    }

    public async Task SaveAsync()
    {
        var result = await _requests.UpdateAsync(_saved.Id, ApplyEdits, CancellationToken.None);
        if (!result.IsSuccess)
        {
            SaveError = ErrorMessageFormatter.Format(result.Error!);
            return;
        }

        _saved = ApplyEdits(_saved);
        _isBodyEdited = false;
        SaveError = string.Empty;
        IsDirty = false;
    }

    public void Cancel()
    {
        _cancellation?.Cancel();
    }

    private void EditField<T>(ref T field, T value, string propertyName)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            IsDirty = true;
        }
    }

    private void EditBodyField(ref string field, string value, string propertyName)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            _isBodyEdited = true;
            IsDirty = true;
        }
    }

    private void ShowResult(Result<ResponseSnapshot> result)
    {
        if (result.IsSuccess)
        {
            Response.Show(result.Value!);
            return;
        }

        Response.ShowError(ErrorMessageFormatter.Format(result.Error!));
    }

    // Fields the editor does not show (headers, query, form and multipart bodies, auth) stay as stored.
    private RequestDefinition ApplyEdits(RequestDefinition definition)
    {
        return definition with
        {
            Method = Method,
            Url = Url.Trim(),
            Body = _isBodyEdited ? CreateBody() : definition.Body,
        };
    }

    private BodyDefinition CreateBody()
    {
        return Body.Length == 0 ? new NoBody() : new RawBody(ContentType.Trim(), Body);
    }
}
