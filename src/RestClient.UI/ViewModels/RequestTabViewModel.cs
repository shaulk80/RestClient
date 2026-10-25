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
    private readonly IDestinationPicker _destinations;
    private readonly AsyncRelayCommand _sendCommand;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly RelayCommand _cancelCommand;
    private RequestDefinition _saved;
    private bool _isLinked;
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

    // An unlinked tab holds a request that is not in any collection yet. It starts dirty, because it has to be saved.
    public RequestTabViewModel(
        IRequestSender sender,
        IRequestService requests,
        IDestinationPicker destinations,
        RequestDefinition definition,
        bool isLinked)
    {
        _sender = sender;
        _requests = requests;
        _destinations = destinations;
        _saved = definition;
        _isLinked = isLinked;
        _isDirty = !isLinked;
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

    // The saved request's identifier. It means nothing for an unlinked tab, so check IsLinked first.
    public Guid RequestId => _saved.Id;

    public bool IsLinked => _isLinked;

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

    // Raised after every send, including failed and cancelled ones, because each one adds a history entry.
    public event EventHandler? RequestSent;

    // Raised when an unlinked tab is saved into a collection.
    public event EventHandler<RequestLinkedEventArgs>? Linked;

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
            RequestSent?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _cancellation = null;
            IsSending = false;
        }
    }

    public async Task SaveAsync()
    {
        if (IsLinked)
        {
            await SaveLinkedAsync();
            return;
        }

        await SaveAsNewRequestAsync();
    }

    public void Cancel()
    {
        _cancellation?.Cancel();
    }

    private async Task SaveLinkedAsync()
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

    // Cancelling the dialog leaves the tab unchanged. A created request is linked even if the tree later renames it.
    private async Task SaveAsNewRequestAsync()
    {
        var picked = await _destinations.PickAsync(Name, CancellationToken.None);
        if (!picked.IsSuccess)
        {
            SaveError = ErrorMessageFormatter.Format(picked.Error!);
            return;
        }

        if (picked.Value is not { } destination)
        {
            return;
        }

        var edited = ApplyEdits(_saved) with { Name = destination.Name };
        var created = await _requests.AddCopyAsync(destination.FolderId, edited, CancellationToken.None);
        if (!created.IsSuccess)
        {
            SaveError = ErrorMessageFormatter.Format(created.Error!);
            return;
        }

        Link(edited with { Id = created.Value! }, destination);
    }

    private void Link(RequestDefinition saved, SaveDestination destination)
    {
        _saved = saved;
        _isLinked = true;
        _isBodyEdited = false;
        Name = saved.Name;
        SaveError = string.Empty;
        IsDirty = false;
        RaisePropertyChanged(nameof(IsLinked));
        Linked?.Invoke(this, new RequestLinkedEventArgs(saved.Id, destination.ExpandId));
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
