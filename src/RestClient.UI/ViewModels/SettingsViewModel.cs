using System.Globalization;
using System.Windows.Input;
using RestClient.Application.Settings;
using RestClient.UI.Commands;
using RestClient.UI.Services;

namespace RestClient.UI.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly AsyncRelayCommand _saveCommand;
    private string _timeoutText = string.Empty;
    private bool _followRedirects;
    private string _maxRedirectsText = string.Empty;
    private bool _verifySsl;
    private string _proxyUrl = string.Empty;
    private string _error = string.Empty;

    public SettingsViewModel(ISettingsService settings)
    {
        _settings = settings;
        _saveCommand = new AsyncRelayCommand(SaveAsync, () => true);
    }

    public string TimeoutText
    {
        get => _timeoutText;
        set => SetProperty(ref _timeoutText, value);
    }

    public bool FollowRedirects
    {
        get => _followRedirects;
        set => SetProperty(ref _followRedirects, value);
    }

    public string MaxRedirectsText
    {
        get => _maxRedirectsText;
        set => SetProperty(ref _maxRedirectsText, value);
    }

    public bool VerifySsl
    {
        get => _verifySsl;
        set => SetProperty(ref _verifySsl, value);
    }

    public string ProxyUrl
    {
        get => _proxyUrl;
        set => SetProperty(ref _proxyUrl, value);
    }

    public string Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    public ICommand SaveCommand => _saveCommand;

    // Raised after the settings were saved. The window closes on it.
    public event EventHandler? Saved;

    public async Task LoadAsync()
    {
        var loaded = await _settings.GetAsync(CancellationToken.None);
        if (!loaded.IsSuccess)
        {
            Error = ErrorMessageFormatter.Format(loaded.Error!);
            Show(AppSettings.Default);
            return;
        }

        Show(loaded.Value!);
    }

    public async Task SaveAsync()
    {
        var settings = Read();
        if (settings is null)
        {
            return;
        }

        var saved = await _settings.SaveAsync(settings, CancellationToken.None);
        if (!saved.IsSuccess)
        {
            Error = ErrorMessageFormatter.Format(saved.Error!);
            return;
        }

        Error = string.Empty;
        Saved?.Invoke(this, EventArgs.Empty);
    }

    private void Show(AppSettings settings)
    {
        TimeoutText = settings.TimeoutSeconds.ToString(CultureInfo.InvariantCulture);
        FollowRedirects = settings.FollowRedirects;
        MaxRedirectsText = settings.MaxRedirects.ToString(CultureInfo.InvariantCulture);
        VerifySsl = settings.VerifySsl;
        ProxyUrl = settings.ProxyUrl;
    }

    // Sets Error and gives null when a number box does not hold a whole number.
    private AppSettings? Read()
    {
        if (!int.TryParse(TimeoutText, NumberStyles.None, CultureInfo.InvariantCulture, out var timeout))
        {
            Error = "The timeout must be a whole number of seconds.";
            return null;
        }

        if (!int.TryParse(MaxRedirectsText, NumberStyles.None, CultureInfo.InvariantCulture, out var maxRedirects))
        {
            Error = "The redirect limit must be a whole number.";
            return null;
        }

        return new AppSettings
        {
            TimeoutSeconds = timeout,
            FollowRedirects = FollowRedirects,
            MaxRedirects = maxRedirects,
            VerifySsl = VerifySsl,
            ProxyUrl = ProxyUrl.Trim(),
        };
    }
}
