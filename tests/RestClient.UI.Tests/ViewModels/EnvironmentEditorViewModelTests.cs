using FakeItEasy;
using NUnit.Framework;
using RestClient.Application.Environments;
using RestClient.Domain.Environments;
using RestClient.Domain.Results;
using RestClient.UI.Services;
using RestClient.UI.ViewModels;

namespace RestClient.UI.Tests.ViewModels;

[TestFixture]
public class EnvironmentEditorViewModelTests
{
    private static readonly Guid StagingId = Guid.NewGuid();

    private IEnvironmentService _environments = null!;
    private IConfirmationDialogs _confirmations = null!;
    private EnvironmentEditorViewModel _viewModel = null!;

    [SetUp]
    public async Task SetUp()
    {
        _environments = A.Fake<IEnvironmentService>();
        A.CallTo(() => _environments.ListAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<IReadOnlyList<RequestEnvironment>>.Success(
                [new RequestEnvironment(StagingId, "Staging", [])])));
        A.CallTo(() => _environments.DeleteAsync(A<Guid>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Success()));
        _confirmations = A.Fake<IConfirmationDialogs>();
        A.CallTo(() => _confirmations.ConfirmAsync(A<string>._, A<CancellationToken>._)).Returns(Task.FromResult(true));
        _viewModel = new EnvironmentEditorViewModel(_environments, A.Fake<ISecretPrompt>(), _confirmations);
        await _viewModel.ReloadAsync();
    }

    [Test]
    public async Task DeleteRemovesEnvironmentWhenUserConfirms()
    {
        await _viewModel.DeleteAsync();

        A.CallTo(() => _environments.DeleteAsync(StagingId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task DeleteDoesNothingWhenUserCancels()
    {
        A.CallTo(() => _confirmations.ConfirmAsync(A<string>._, A<CancellationToken>._)).Returns(Task.FromResult(false));

        await _viewModel.DeleteAsync();

        A.CallTo(() => _environments.DeleteAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
