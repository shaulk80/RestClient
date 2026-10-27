using System.Windows.Input;
using RestClient.Application.Abstractions;
using RestClient.Application.Collections;
using RestClient.Application.Tabs;
using RestClient.Domain.Collections;
using RestClient.Domain.Responses;
using RestClient.Domain.Results;
using RestClient.UI.Commands;
using RestClient.UI.Services;

namespace RestClient.UI.ViewModels;

public sealed class RequestTabViewModel : ViewModelBase
{
    private static readonly IReadOnlyList<RequestMethod> AllMethods = Enum.GetValues<RequestMethod>();

    private readonly IRequestSender _sender;
    private readonly IRequestService _requests;
    private readonly IDestinationPicker _destinations;
    private readonly ISecretPrompt _secrets;
    private readonly AsyncRelayCommand _sendCommand;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly RelayCommand _cancelCommand;
    private readonly RelayCommand _closeCommand;
    private RequestDefinition _saved;
    private bool _isLinked;
    private CancellationTokenSource? _cancellation;
    private string _name;
    private RequestMethod _method;
    private string _url;
    private bool _isSending;
    private bool _isDirty;
    private string _saveError = string.Empty;
    private int _sectionIndex = (int)RequestSection.Body;

    // An unlinked tab holds a request that is not in any collection yet. It starts dirty, because it has to be saved.
    // A restored tab with unsaved edits is passed as dirty.
    public RequestTabViewModel(RequestTabServices services, RequestDefinition definition, bool isLinked, bool? isDirty = null)
    {
        _sender = services.Sender;
        _requests = services.Requests;
        _destinations = services.Destinations;
        _secrets = services.SecretPrompt;
        _saved = definition;
        _isLinked = isLinked;
        _isDirty = isDirty ?? !isLinked;
        _name = definition.Name;
        _method = definition.Method;
        _url = definition.Url;
        QueryParams = new KeyValueEditorViewModel(definition.QueryParams, "Parameter");
        QueryParams.Changed += OnBodyEdited;
        Headers = new KeyValueEditorViewModel(definition.Headers, "Header");
        Headers.Changed += OnBodyEdited;
        BodyEditor = new BodyEditorViewModel(services.Formatting, services.Files, definition.Body);
        BodyEditor.Edited += OnBodyEdited;
        AuthEditor = new AuthEditorViewModel(definition.Auth);
        AuthEditor.Edited += OnBodyEdited;
        Response = new ResponseViewModel(services.Formatting);
        _sendCommand = new AsyncRelayCommand(SendAsync, () => !IsSending);
        _saveCommand = new AsyncRelayCommand(SaveAsync, () => true);
        _cancelCommand = new RelayCommand(Cancel, () => IsSending);
        _closeCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty), () => true);
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

    // Shared by all tabs, see BodyEditorViewModel.Types.
    public IReadOnlyList<RequestMethod> Methods { get; } = AllMethods;

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

    // The page of the request editor that is shown, as the index of a RequestSection. Each tab keeps its own page,
    // and a tab starts on the body. Choosing a page is not an edit.
    public int SectionIndex
    {
        get => _sectionIndex;
        set => SetProperty(ref _sectionIndex, value);
    }

    public KeyValueEditorViewModel QueryParams { get; }

    public KeyValueEditorViewModel Headers { get; }

    public BodyEditorViewModel BodyEditor { get; }

    public AuthEditorViewModel AuthEditor { get; }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetProperty(ref _isDirty, value))
            {
                RaisePropertyChanged(nameof(Header));
                Changed?.Invoke(this, EventArgs.Empty);
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

    public ResponseViewModel Response { get; }

    public ICommand SendCommand => _sendCommand;

    public ICommand SaveCommand => _saveCommand;

    public ICommand CancelCommand => _cancelCommand;

    public ICommand CloseCommand => _closeCommand;

    // Raised on every edit and whenever the dirty state changes, so the open tabs can be stored again.
    public event EventHandler? Changed;

    // Raised when the user asks to close the tab. The tab strip decides whether it closes.
    public event EventHandler? CloseRequested;

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
            var result = await SendOnceAsync(cancellation.Token);
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

    // The request as edited now, including changes that are not saved yet.
    public TabSnapshot Snapshot(bool isSelected)
    {
        return new TabSnapshot(ApplyEdits(_saved), IsLinked, IsDirty, isSelected);
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
            MarkEdited();
        }
    }

    private void OnBodyEdited(object? sender, EventArgs e)
    {
        MarkEdited();
    }

    private void MarkEdited()
    {
        IsDirty = true;
        Changed?.Invoke(this, EventArgs.Empty);
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

    // A send that needs a locked secret asks for the password once, then sends again. Other failures are shown as they are.
    private async Task<Result<ResponseSnapshot>> SendOnceAsync(CancellationToken cancellationToken)
    {
        var result = await _sender.SendAsync(ApplyEdits(_saved), cancellationToken);
        if (result.Error?.Category != ErrorCategory.Security)
        {
            return result;
        }

        return await RetryAfterUnlockAsync(result, cancellationToken);
    }

    private async Task<Result<ResponseSnapshot>> RetryAfterUnlockAsync(
        Result<ResponseSnapshot> failed,
        CancellationToken cancellationToken)
    {
        var unlocked = await _secrets.EnsureUnlockedAsync(cancellationToken);
        if (!unlocked.IsSuccess || !unlocked.Value)
        {
            return failed;
        }

        return await _sender.SendAsync(ApplyEdits(_saved), cancellationToken);
    }

    private RequestDefinition ApplyEdits(RequestDefinition definition)
    {
        return definition with
        {
            Method = Method,
            Url = Url.Trim(),
            QueryParams = QueryParams.ToEntries(),
            Headers = Headers.ToEntries(),
            Body = BodyEditor.Build(),
            Auth = AuthEditor.Build(),
        };
    }
}
