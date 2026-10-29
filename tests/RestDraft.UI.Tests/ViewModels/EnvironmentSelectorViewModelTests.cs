using FakeItEasy;
using NUnit.Framework;
using RestDraft.Application.Environments;
using RestDraft.Domain.Environments;
using RestDraft.Domain.Results;
using RestDraft.UI.Services;
using RestDraft.UI.ViewModels;

namespace RestDraft.UI.Tests.ViewModels;

[TestFixture]
public class EnvironmentSelectorViewModelTests
{
    private static readonly string[] ExpectedNames = ["No environment", "Staging"];

    private IEnvironmentService _environments = null!;
    private EnvironmentSelectorViewModel _selector = null!;
    private RequestEnvironment _staging = null!;

    [SetUp]
    public void SetUp()
    {
        _staging = new RequestEnvironment(Guid.NewGuid(), "Staging", []);
        _environments = A.Fake<IEnvironmentService>();
        A.CallTo(() => _environments.ListAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<IReadOnlyList<RequestEnvironment>>.Success([_staging])));
        A.CallTo(() => _environments.GetActiveAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<RequestEnvironment?>.Success(_staging)));
        A.CallTo(() => _environments.SetActiveAsync(A<Guid?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Success()));
        _selector = new EnvironmentSelectorViewModel(_environments, A.Fake<IEnvironmentDialogs>());
    }

    [Test]
    public async Task ReloadListsNoEnvironmentFirstAndSelectsTheActiveOne()
    {
        await _selector.ReloadAsync();

        Assert.That(_selector.Choices.Select(choice => choice.Name), Is.EqualTo(ExpectedNames));
        Assert.That(_selector.SelectedChoice?.Id, Is.EqualTo(_staging.Id));
    }

    [Test]
    public async Task PickingAnotherChoiceStoresItAsActive()
    {
        await _selector.ReloadAsync();
        var none = _selector.Choices[0];

        await _selector.SelectAsync(none);

        A.CallTo(() => _environments.SetActiveAsync(null, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        Assert.That(_selector.SelectedChoice, Is.EqualTo(none));
    }

    [Test]
    public async Task SelectionReportedAfterReloadIsNotStoredAgain()
    {
        await _selector.ReloadAsync();

        await _selector.SelectAsync(_selector.SelectedChoice);

        A.CallTo(() => _environments.SetActiveAsync(A<Guid?>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task FailedStoreShowsMessage()
    {
        await _selector.ReloadAsync();
        A.CallTo(() => _environments.SetActiveAsync(A<Guid?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Failure(new Error(ErrorCategory.Storage, "Disk full."))));

        await _selector.SelectAsync(_selector.Choices[0]);

        Assert.That(_selector.Message, Is.Not.Empty);
    }
}
