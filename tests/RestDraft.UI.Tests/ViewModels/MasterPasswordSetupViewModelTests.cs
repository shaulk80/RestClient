using FakeItEasy;
using NUnit.Framework;
using RestDraft.Application.Secrets;
using RestDraft.Domain.Results;
using RestDraft.UI.ViewModels;

namespace RestDraft.UI.Tests.ViewModels;

[TestFixture]
public class MasterPasswordSetupViewModelTests
{
    private ISecretService _secrets = null!;
    private MasterPasswordSetupViewModel _viewModel = null!;

    [SetUp]
    public void SetUp()
    {
        _secrets = A.Fake<ISecretService>();
        _viewModel = new MasterPasswordSetupViewModel(_secrets);
    }

    [Test]
    public async Task MismatchedConfirmationShowsMessageAndSetsNothing()
    {
        _viewModel.Password = "one";
        _viewModel.Confirmation = "two";

        var created = await _viewModel.CreateAsync();

        Assert.That(created, Is.False);
        Assert.That(_viewModel.Message, Is.EqualTo("The passwords do not match."));
        A.CallTo(() => _secrets.SetUpAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task MatchingPasswordsSetUpAndRaiseCreated()
    {
        A.CallTo(() => _secrets.SetUpAsync("same", A<CancellationToken>._)).Returns(Task.FromResult(Result.Success()));
        var raised = false;
        _viewModel.Created += (_, _) => raised = true;
        _viewModel.Password = "same";
        _viewModel.Confirmation = "same";

        var created = await _viewModel.CreateAsync();

        Assert.That(created, Is.True);
        Assert.That(raised, Is.True);
    }

    [Test]
    public async Task SetUpFailureIsShownAndNotCreated()
    {
        A.CallTo(() => _secrets.SetUpAsync("same", A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Failure(ResultErrors.Validation("Enter a master password."))));
        _viewModel.Password = "same";
        _viewModel.Confirmation = "same";

        var created = await _viewModel.CreateAsync();

        Assert.That(created, Is.False);
        Assert.That(_viewModel.Message, Is.EqualTo("Enter a master password."));
    }
}
