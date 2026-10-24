using System.Windows.Input;
using RestClient.Application.Abstractions;
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
    private readonly Guid _definitionId;
    private readonly AsyncRelayCommand _sendCommand;
    private readonly RelayCommand _cancelCommand;
    private CancellationTokenSource? _cancellation;
    private RequestMethod _method = RequestMethod.Get;
    private string _url = string.Empty;
    private string _contentType = string.Empty;
    private string _body = string.Empty;
    private bool _isSending;

    public RequestTabViewModel(IRequestSender sender, IIdGenerator idGenerator)
    {
        _sender = sender;
        _definitionId = idGenerator.NewId();
        _sendCommand = new AsyncRelayCommand(SendAsync, () => !IsSending);
        _cancelCommand = new RelayCommand(Cancel, () => IsSending);
    }

    public IReadOnlyList<RequestMethod> Methods { get; } = Enum.GetValues<RequestMethod>();

    public RequestMethod Method
    {
        get => _method;
        set => SetProperty(ref _method, value);
    }

    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }

    public string ContentType
    {
        get => _contentType;
        set => SetProperty(ref _contentType, value);
    }

    public string Body
    {
        get => _body;
        set => SetProperty(ref _body, value);
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

    public ResponseViewModel Response { get; } = new();

    public ICommand SendCommand => _sendCommand;

    public ICommand CancelCommand => _cancelCommand;

    public async Task SendAsync()
    {
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        IsSending = true;
        Response.Clear();
        try
        {
            var result = await _sender.SendAsync(CreateDefinition(), NoVariables, cancellation.Token);
            ShowResult(result);
        }
        finally
        {
            _cancellation = null;
            IsSending = false;
        }
    }

    public void Cancel()
    {
        _cancellation?.Cancel();
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

    private RequestDefinition CreateDefinition()
    {
        return new RequestDefinition
        {
            Id = _definitionId,
            Method = Method,
            Url = Url.Trim(),
            Body = CreateBody(),
        };
    }

    private BodyDefinition CreateBody()
    {
        return Body.Length == 0 ? new NoBody() : new RawBody(ContentType.Trim(), Body);
    }
}
