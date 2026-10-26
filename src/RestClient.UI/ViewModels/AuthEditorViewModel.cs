using RestClient.Domain.Auth;

namespace RestClient.UI.ViewModels;

public sealed class AuthEditorViewModel : ViewModelBase
{
    private AuthType _type;
    private string _username;
    private string _password;
    private string _token;
    private string _apiKeyName;
    private string _apiKeyValue;
    private ApiKeyLocation _apiKeyLocation;

    public AuthEditorViewModel(AuthDefinition auth)
    {
        _type = TypeOf(auth);
        _username = auth is BasicAuth basic ? basic.Username : string.Empty;
        _password = auth is BasicAuth stored ? stored.Password : string.Empty;
        _token = auth is BearerAuth bearer ? bearer.Token : string.Empty;
        _apiKeyName = auth is ApiKeyAuth apiKey ? apiKey.Key : string.Empty;
        _apiKeyValue = auth is ApiKeyAuth apiKeyValue ? apiKeyValue.Value : string.Empty;
        _apiKeyLocation = auth is ApiKeyAuth location ? location.Location : ApiKeyLocation.Header;
    }

    // Raised after any change a user makes. The tab uses it to mark itself dirty.
    public event EventHandler? Edited;

    public IReadOnlyList<AuthType> Types { get; } = Enum.GetValues<AuthType>();

    public IReadOnlyList<ApiKeyLocation> Locations { get; } = Enum.GetValues<ApiKeyLocation>();

    public AuthType Type
    {
        get => _type;
        set
        {
            if (SetProperty(ref _type, value))
            {
                RaiseTypeFlags();
                OnEdited();
            }
        }
    }

    public bool IsBasic => Type == AuthType.Basic;

    public bool IsBearer => Type == AuthType.Bearer;

    public bool IsApiKey => Type == AuthType.ApiKey;

    public string Username
    {
        get => _username;
        set => EditField(ref _username, value, nameof(Username));
    }

    public string Password
    {
        get => _password;
        set => EditField(ref _password, value, nameof(Password));
    }

    public string Token
    {
        get => _token;
        set => EditField(ref _token, value, nameof(Token));
    }

    public string ApiKeyName
    {
        get => _apiKeyName;
        set => EditField(ref _apiKeyName, value, nameof(ApiKeyName));
    }

    public string ApiKeyValue
    {
        get => _apiKeyValue;
        set => EditField(ref _apiKeyValue, value, nameof(ApiKeyValue));
    }

    public ApiKeyLocation ApiKeyLocation
    {
        get => _apiKeyLocation;
        set => EditField(ref _apiKeyLocation, value, nameof(ApiKeyLocation));
    }

    public AuthDefinition Build()
    {
        return Type switch
        {
            AuthType.Basic => new BasicAuth(Username, Password),
            AuthType.Bearer => new BearerAuth(Token),
            AuthType.ApiKey => new ApiKeyAuth(ApiKeyName, ApiKeyValue, ApiKeyLocation),
            _ => new NoAuth(),
        };
    }

    private static AuthType TypeOf(AuthDefinition auth)
    {
        return auth switch
        {
            BasicAuth => AuthType.Basic,
            BearerAuth => AuthType.Bearer,
            ApiKeyAuth => AuthType.ApiKey,
            _ => AuthType.None,
        };
    }

    private void EditField<T>(ref T field, T value, string propertyName)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            OnEdited();
        }
    }

    private void RaiseTypeFlags()
    {
        RaisePropertyChanged(nameof(IsBasic));
        RaisePropertyChanged(nameof(IsBearer));
        RaisePropertyChanged(nameof(IsApiKey));
    }

    private void OnEdited()
    {
        Edited?.Invoke(this, EventArgs.Empty);
    }
}
