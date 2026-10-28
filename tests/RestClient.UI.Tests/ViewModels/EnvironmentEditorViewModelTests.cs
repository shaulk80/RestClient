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
    private static readonly Guid ProductionId = Guid.NewGuid();
    private static readonly bool[] ProductionIsCurrent = [false, true];
    private static readonly bool[] StagingIsCurrent = [true, false];

    private IEnvironmentService _environments = null!;
    private IConfirmationDialogs _confirmations = null!;
    private EnvironmentEditorViewModel _viewModel = null!;

    [SetUp]
    public async Task SetUp()
    {
        _environments = A.Fake<IEnvironmentService>();
        A.CallTo(() => _environments.ListAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<IReadOnlyList<RequestEnvironment>>.Success(
                [new RequestEnvironment(StagingId, "Staging", []), new RequestEnvironment(ProductionId, "Production", [])])));
        A.CallTo(() => _environments.GetActiveAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<RequestEnvironment?>.Success(new RequestEnvironment(ProductionId, "Production", []))));
        A.CallTo(() => _environments.DeleteAsync(A<Guid>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Success()));
        A.CallTo(() => _environments.RenameAsync(A<Guid>._, A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Success()));
        A.CallTo(() => _environments.SetActiveAsync(A<Guid?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Success()));
        A.CallTo(() => _environments.CreateAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<Guid>.Success(StagingId)));
        _confirmations = A.Fake<IConfirmationDialogs>();
        A.CallTo(() => _confirmations.ConfirmAsync(A<string>._, A<CancellationToken>._)).Returns(Task.FromResult(true));
        _viewModel = new EnvironmentEditorViewModel(_environments, A.Fake<ISecretPrompt>(), _confirmations);
        await _viewModel.ReloadAsync();
    }

    [Test]
    public void ListMarksTheCurrentEnvironment()
    {
        Assert.That(_viewModel.Environments.Select(item => item.IsCurrent), Is.EqualTo(ProductionIsCurrent));
        Assert.That(_viewModel.Environments[1].AutomationName, Is.EqualTo("Production, current environment"));
        Assert.That(_viewModel.Environments[0].AutomationName, Is.EqualTo("Staging"));
    }

    [Test]
    public async Task SetCurrentStoresTheSelectedEnvironmentAndMovesTheMark()
    {
        _viewModel.SelectedEnvironment = _viewModel.Environments[0];

        var done = await _viewModel.SetCurrentAsync();

        Assert.That(done, Is.True);
        A.CallTo(() => _environments.SetActiveAsync(StagingId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        Assert.That(_viewModel.Environments.Select(item => item.IsCurrent), Is.EqualTo(StagingIsCurrent));
    }

    [Test]
    public void SetCurrentIsNotOfferedForTheEnvironmentThatIsCurrent()
    {
        _viewModel.SelectedEnvironment = _viewModel.Environments[1];

        Assert.That(_viewModel.SetCurrentCommand.CanExecute(null), Is.False);
    }

    [Test]
    public async Task FailureToSetCurrentShowsAMessageAndKeepsTheMark()
    {
        A.CallTo(() => _environments.SetActiveAsync(A<Guid?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Failure(new Error(ErrorCategory.Storage, "The setting could not be saved."))));
        _viewModel.SelectedEnvironment = _viewModel.Environments[0];

        var done = await _viewModel.SetCurrentAsync();

        Assert.That(done, Is.False);
        Assert.That(_viewModel.Message, Is.Not.Empty);
        Assert.That(_viewModel.Environments.Select(item => item.IsCurrent), Is.EqualTo(ProductionIsCurrent));
    }

    [Test]
    public async Task CreateAddsAnEnvironmentWithADefaultNameAndStartsRenamingIt()
    {
        var created = await _viewModel.CreateAsync();

        Assert.That(created, Is.True);
        A.CallTo(() => _environments.CreateAsync("New environment", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        Assert.That(_viewModel.SelectedEnvironment?.Id, Is.EqualTo(StagingId));
        Assert.That(_viewModel.SelectedEnvironment?.IsEditing, Is.True);
    }

    [Test]
    public async Task FailureToCreateShowsAMessage()
    {
        A.CallTo(() => _environments.CreateAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<Guid>.Failure(new Error(ErrorCategory.Storage, "The environments could not be saved."))));

        var created = await _viewModel.CreateAsync();

        Assert.That(created, Is.False);
        Assert.That(_viewModel.Message, Is.Not.Empty);
    }

    [Test]
    public void RenameStartsEditingTheSelectedEnvironmentWithItsName()
    {
        _viewModel.SelectedEnvironment = _viewModel.Environments[0];

        _viewModel.RenameCommand.Execute(null);

        Assert.That(_viewModel.Environments[0].IsEditing, Is.True);
        Assert.That(_viewModel.Environments[0].EditName, Is.EqualTo("Staging"));
    }

    [Test]
    public async Task CommittingARenameStoresTheTrimmedName()
    {
        var item = _viewModel.Environments[0];
        item.BeginRename();
        item.EditName = "  QA  ";

        var renamed = await _viewModel.CommitRenameAsync(item);

        Assert.That(renamed, Is.True);
        A.CallTo(() => _environments.RenameAsync(StagingId, "QA", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        Assert.That(item.IsEditing, Is.False);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("Staging")]
    public async Task CommittingAnEmptyOrUnchangedNameStoresNothing(string name)
    {
        var item = _viewModel.Environments[0];
        item.BeginRename();
        item.EditName = name;

        var renamed = await _viewModel.CommitRenameAsync(item);

        Assert.That(renamed, Is.False);
        Assert.That(item.IsEditing, Is.False);
        A.CallTo(() => _environments.RenameAsync(A<Guid>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task CommitAfterTheRenameIsOverDoesNothing()
    {
        var item = _viewModel.Environments[0];
        item.BeginRename();
        item.EditName = "QA";
        item.CancelRenameCommand.Execute(null);

        var renamed = await _viewModel.CommitRenameAsync(item);

        Assert.That(renamed, Is.False);
        A.CallTo(() => _environments.RenameAsync(A<Guid>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task FailureToRenameShowsAMessage()
    {
        A.CallTo(() => _environments.RenameAsync(A<Guid>._, A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Failure(ResultErrors.Validation("An environment named 'QA' already exists."))));
        var item = _viewModel.Environments[0];
        item.BeginRename();
        item.EditName = "QA";

        var renamed = await _viewModel.CommitRenameAsync(item);

        Assert.That(renamed, Is.False);
        Assert.That(_viewModel.Message, Does.Contain("already exists"));
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

    [TestCase(new string[0], "New environment")]
    [TestCase(new[] { "Staging" }, "New environment")]
    [TestCase(new[] { "New environment" }, "New environment 2")]
    [TestCase(new[] { "new environment", "New environment 2" }, "New environment 3")]
    public void NewEnvironmentNameSkipsTheNamesInUse(string[] existing, string expected)
    {
        Assert.That(NewEnvironmentName.NextAvailable(existing), Is.EqualTo(expected));
    }
}
