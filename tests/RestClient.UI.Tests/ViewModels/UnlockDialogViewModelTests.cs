using FakeItEasy;
using NUnit.Framework;
using RestClient.Application.Secrets;
using RestClient.Domain.Results;
using RestClient.UI.ViewModels;

namespace RestClient.UI.Tests.ViewModels;

[TestFixture]
public class UnlockDialogViewModelTests
{
    private ISecretService _secrets = null!;
    private UnlockDialogViewModel _viewModel = null!;

    [SetUp]
    public void SetUp()
    {
        _secrets = A.Fake<ISecretService>();
        _viewModel = new UnlockDialogViewModel(_secrets);
    }

    [Test]
    public async Task CorrectPasswordRaisesUnlockedAndClearsPassword()
    {
        A.CallTo(() => _secrets.UnlockAsync("secret", A<CancellationToken>._)).Returns(Task.FromResult(Result.Success()));
        var raised = false;
        _viewModel.Unlocked += (_, _) => raised = true;
        _viewModel.Password = "secret";

        var unlocked = await _viewModel.UnlockAsync();

        Assert.That(unlocked, Is.True);
        Assert.That(raised, Is.True);
        Assert.That(_viewModel.Password, Is.Empty);
    }

    [Test]
    public async Task WrongPasswordShowsMessageAndDoesNotRaiseUnlocked()
    {
        A.CallTo(() => _secrets.UnlockAsync("wrong", A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Failure(new Error(ErrorCategory.Security, "The master password is wrong."))));
        var raised = false;
        _viewModel.Unlocked += (_, _) => raised = true;
        _viewModel.Password = "wrong";

        var unlocked = await _viewModel.UnlockAsync();

        Assert.That(unlocked, Is.False);
        Assert.That(raised, Is.False);
        Assert.That(_viewModel.Message, Is.EqualTo("The master password is wrong."));
    }

    [TestCase("", false)]
    [TestCase("x", true)]
    public void UnlockCommandIsEnabledOnlyWhenPasswordIsEntered(string password, bool expected)
    {
        _viewModel.Password = password;

        Assert.That(_viewModel.UnlockCommand.CanExecute(null), Is.EqualTo(expected));
    }
}
