using NUnit.Framework;
using RestClient.Domain.Auth;
using RestClient.UI.ViewModels;

namespace RestClient.UI.Tests.ViewModels;

[TestFixture]
public class AuthEditorViewModelTests
{
    [Test]
    public void NoAuthBuildsNoAuth()
    {
        var viewModel = new AuthEditorViewModel(new NoAuth());

        Assert.That(viewModel.Build(), Is.EqualTo(new NoAuth()));
    }

    [Test]
    public void BasicAuthBuildsFromEnteredValues()
    {
        var viewModel = new AuthEditorViewModel(new NoAuth())
        {
            Type = AuthType.Basic,
            Username = "user",
            Password = "secret",
        };

        Assert.That(viewModel.Build(), Is.EqualTo(new BasicAuth("user", "secret")));
    }

    [Test]
    public void BearerAuthBuildsFromEnteredToken()
    {
        var viewModel = new AuthEditorViewModel(new NoAuth())
        {
            Type = AuthType.Bearer,
            Token = "{{token}}",
        };

        Assert.That(viewModel.Build(), Is.EqualTo(new BearerAuth("{{token}}")));
    }

    [Test]
    public void ApiKeyAuthBuildsWithLocation()
    {
        var viewModel = new AuthEditorViewModel(new NoAuth())
        {
            Type = AuthType.ApiKey,
            ApiKeyName = "api_key",
            ApiKeyValue = "{{key}}",
            ApiKeyLocation = ApiKeyLocation.Query,
        };

        Assert.That(viewModel.Build(), Is.EqualTo(new ApiKeyAuth("api_key", "{{key}}", ApiKeyLocation.Query)));
    }

    [TestCase(AuthType.Basic)]
    [TestCase(AuthType.Bearer)]
    [TestCase(AuthType.ApiKey)]
    public void FlagsShowOnlyTheSelectedType(AuthType type)
    {
        var viewModel = new AuthEditorViewModel(new NoAuth()) { Type = type };

        Assert.That(viewModel.IsBasic, Is.EqualTo(type == AuthType.Basic));
        Assert.That(viewModel.IsBearer, Is.EqualTo(type == AuthType.Bearer));
        Assert.That(viewModel.IsApiKey, Is.EqualTo(type == AuthType.ApiKey));
    }

    [Test]
    public void ExistingAuthIsLoadedAndBuiltBackUnchanged()
    {
        var original = new ApiKeyAuth("X-Key", "value", ApiKeyLocation.Header);

        var viewModel = new AuthEditorViewModel(original);

        Assert.That(viewModel.Build(), Is.EqualTo(original));
    }

    [Test]
    public void ChangingAFieldRaisesEdited()
    {
        var viewModel = new AuthEditorViewModel(new NoAuth());
        var raised = 0;
        viewModel.Edited += (_, _) => raised++;

        viewModel.Type = AuthType.Bearer;
        viewModel.Token = "t";

        Assert.That(raised, Is.EqualTo(2));
    }
}
