using FakeItEasy;
using NUnit.Framework;
using RestClient.Application.Settings;
using RestClient.Domain.Results;
using RestClient.UI.ViewModels;

namespace RestClient.UI.Tests.ViewModels;

[TestFixture]
public class SettingsViewModelTests
{
    private ISettingsService _settings = null!;
    private SettingsViewModel _viewModel = null!;
    private AppSettings? _saved;

    [SetUp]
    public void SetUp()
    {
        _saved = null;
        _settings = A.Fake<ISettingsService>();
        A.CallTo(() => _settings.GetAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<AppSettings>.Success(AppSettings.Default)));
        A.CallTo(() => _settings.SaveAsync(A<AppSettings>._, A<CancellationToken>._))
            .Invokes((AppSettings settings, CancellationToken _) => _saved = settings)
            .Returns(Task.FromResult(Result.Success()));
        _viewModel = new SettingsViewModel(_settings);
    }

    [Test]
    public async Task LoadShowsTheCurrentSettings()
    {
        var current = new AppSettings
        {
            TimeoutSeconds = 12,
            FollowRedirects = false,
            MaxRedirects = 4,
            VerifySsl = false,
            ProxyUrl = "http://proxy:8080",
            OpenOnSingleClick = true,
        };
        A.CallTo(() => _settings.GetAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<AppSettings>.Success(current)));

        await _viewModel.LoadAsync();

        Assert.That(_viewModel.TimeoutText, Is.EqualTo("12"));
        Assert.That(_viewModel.FollowRedirects, Is.False);
        Assert.That(_viewModel.MaxRedirectsText, Is.EqualTo("4"));
        Assert.That(_viewModel.VerifySsl, Is.False);
        Assert.That(_viewModel.ProxyUrl, Is.EqualTo("http://proxy:8080"));
        Assert.That(_viewModel.OpenOnSingleClick, Is.True);
    }

    [Test]
    public async Task OpenOnSingleClickIsOffByDefault()
    {
        await _viewModel.LoadAsync();

        Assert.That(_viewModel.OpenOnSingleClick, Is.False);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task SaveStoresTheSingleClickChoice(bool choice)
    {
        await _viewModel.LoadAsync();
        _viewModel.OpenOnSingleClick = choice;

        await _viewModel.SaveAsync();

        Assert.That(_saved?.OpenOnSingleClick, Is.EqualTo(choice));
    }

    [Test]
    public async Task SaveKeepsTheSettingsThatHaveNoBox()
    {
        var current = AppSettings.Default with { SidebarWidth = 333, ResponseWidth = 444 };
        A.CallTo(() => _settings.GetAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<AppSettings>.Success(current)));
        await _viewModel.LoadAsync();

        await _viewModel.SaveAsync();

        Assert.That(_saved?.SidebarWidth, Is.EqualTo(333));
        Assert.That(_saved?.ResponseWidth, Is.EqualTo(444));
    }

    [Test]
    public async Task LoadFailureShowsErrorAndDefaults()
    {
        A.CallTo(() => _settings.GetAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<AppSettings>.Failure(new Error(ErrorCategory.Storage, "A setting could not be read."))));

        await _viewModel.LoadAsync();

        Assert.That(_viewModel.Error, Is.EqualTo("A setting could not be read."));
        Assert.That(_viewModel.TimeoutText, Is.EqualTo("30"));
    }

    [Test]
    public async Task SaveSendsTheEditedValuesAndRaisesSaved()
    {
        await _viewModel.LoadAsync();
        _viewModel.TimeoutText = "5";
        _viewModel.MaxRedirectsText = "3";
        _viewModel.VerifySsl = false;
        _viewModel.ProxyUrl = "  http://proxy:8080  ";
        var raised = 0;
        _viewModel.Saved += (_, _) => raised++;

        await _viewModel.SaveAsync();

        Assert.That(_saved, Is.EqualTo(new AppSettings
        {
            TimeoutSeconds = 5,
            MaxRedirects = 3,
            VerifySsl = false,
            ProxyUrl = "http://proxy:8080",
        }));
        Assert.That(raised, Is.EqualTo(1));
        Assert.That(_viewModel.Error, Is.Empty);
    }

    [TestCase("abc", "10")]
    [TestCase("", "10")]
    [TestCase("-1", "10")]
    [TestCase("30", "many")]
    [TestCase("30", "")]
    public async Task SaveRejectsNumbersThatAreNotWholeNumbers(string timeout, string redirects)
    {
        await _viewModel.LoadAsync();
        _viewModel.TimeoutText = timeout;
        _viewModel.MaxRedirectsText = redirects;
        var raised = 0;
        _viewModel.Saved += (_, _) => raised++;

        await _viewModel.SaveAsync();

        Assert.That(_viewModel.Error, Is.Not.Empty);
        Assert.That(raised, Is.EqualTo(0));
        A.CallTo(() => _settings.SaveAsync(A<AppSettings>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task SaveShowsTheServiceError()
    {
        A.CallTo(() => _settings.SaveAsync(A<AppSettings>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Failure(ResultErrors.Validation("The timeout must be between 1 and 3600 seconds."))));
        await _viewModel.LoadAsync();
        var raised = 0;
        _viewModel.Saved += (_, _) => raised++;

        await _viewModel.SaveAsync();

        Assert.That(_viewModel.Error, Is.EqualTo("The timeout must be between 1 and 3600 seconds."));
        Assert.That(raised, Is.EqualTo(0));
    }
}
