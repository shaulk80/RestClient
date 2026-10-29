using FakeItEasy;
using NUnit.Framework;
using RestDraft.Application.Collections;
using RestDraft.Domain.Results;
using RestDraft.UI.ViewModels;

namespace RestDraft.UI.Tests.ViewModels;

[TestFixture]
public class CollectionsTreeViewModelTests
{
    private TreeFixtures _fixtures = null!;

    [SetUp]
    public void SetUp()
    {
        _fixtures = new TreeFixtures();
    }

    [Test]
    public async Task ReloadSelectsRequestedNode()
    {
        var sidebar = _fixtures.CreateSidebar();

        await sidebar.ReloadAsync(_fixtures.Login.Id);

        Assert.That(sidebar.SelectedNode, Is.InstanceOf<RequestNodeViewModel>());
        Assert.That(sidebar.SelectedNode?.Id, Is.EqualTo(_fixtures.Login.Id));
    }

    [Test]
    public async Task ReloadRaisesTreeChanged()
    {
        var sidebar = _fixtures.CreateSidebar();
        var raised = false;
        sidebar.TreeChanged += (_, _) => raised = true;

        await sidebar.ReloadAsync();

        Assert.That(raised, Is.True);
    }

    [Test]
    public async Task ReloadShowsErrorWhenCollectionsCannotBeLoaded()
    {
        var sidebar = _fixtures.CreateSidebar();
        A.CallTo(() => _fixtures.Collections.ListAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<IReadOnlyList<RestDraft.Domain.Collections.Collection>>.Failure(new Error(ErrorCategory.Storage, "Load failed."))));

        await sidebar.ReloadAsync();

        Assert.That(sidebar.StatusMessage, Is.EqualTo("Load failed."));
        Assert.That(sidebar.Roots, Is.Empty);
    }

    [Test]
    public async Task CommitRenameRenamesRequestAndReloads()
    {
        var sidebar = _fixtures.CreateSidebar();
        await sidebar.ReloadAsync();
        var node = sidebar.FindRequest(_fixtures.Login.Id)!;
        node.BeginRename();
        node.EditName = "Sign in";
        A.CallTo(() => _fixtures.Requests.RenameAsync(_fixtures.Login.Id, "Sign in", A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Success()));

        await sidebar.CommitRenameAsync(node);

        Assert.That(node.IsEditing, Is.False);
        Assert.That(sidebar.StatusMessage, Is.Empty);
        A.CallTo(() => _fixtures.Requests.RenameAsync(_fixtures.Login.Id, "Sign in", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [TestCase("Login")]
    [TestCase("   ")]
    public async Task CommitRenameWithUnchangedOrBlankNameDoesNotCallService(string editName)
    {
        var sidebar = _fixtures.CreateSidebar();
        await sidebar.ReloadAsync();
        var node = sidebar.FindRequest(_fixtures.Login.Id)!;
        node.BeginRename();
        node.EditName = editName;

        await sidebar.CommitRenameAsync(node);

        Assert.That(node.IsEditing, Is.False);
        A.CallTo(() => _fixtures.Requests.RenameAsync(A<Guid>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task CommitRenameShowsServiceErrorAndStopsEditing()
    {
        var sidebar = _fixtures.CreateSidebar();
        await sidebar.ReloadAsync();
        var node = sidebar.FindRequest(_fixtures.Login.Id)!;
        node.BeginRename();
        node.EditName = "Duplicate";
        A.CallTo(() => _fixtures.Requests.RenameAsync(A<Guid>._, A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Failure(new Error(ErrorCategory.Validation, "A folder or request named 'Duplicate' already exists here."))));

        await sidebar.CommitRenameAsync(node);

        Assert.That(node.IsEditing, Is.False);
        Assert.That(sidebar.StatusMessage, Is.EqualTo("A folder or request named 'Duplicate' already exists here."));
    }

    [Test]
    public async Task CommitRenameTwiceRenamesOnce()
    {
        var sidebar = _fixtures.CreateSidebar();
        await sidebar.ReloadAsync();
        var node = sidebar.FindRequest(_fixtures.Login.Id)!;
        node.BeginRename();
        node.EditName = "Sign in";
        A.CallTo(() => _fixtures.Requests.RenameAsync(A<Guid>._, A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Success()));

        await sidebar.CommitRenameAsync(node);
        await sidebar.CommitRenameAsync(node);

        A.CallTo(() => _fixtures.Requests.RenameAsync(A<Guid>._, A<string>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task CommitRenameAfterCancelDoesNotRename()
    {
        var sidebar = _fixtures.CreateSidebar();
        await sidebar.ReloadAsync();
        var node = sidebar.FindRequest(_fixtures.Login.Id)!;
        node.BeginRename();
        node.EditName = "Sign in";
        node.CancelRenameCommand.Execute(null);

        await sidebar.CommitRenameAsync(node);

        A.CallTo(() => _fixtures.Requests.RenameAsync(A<Guid>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Test]
    public async Task ToolbarCommandsNeedASelectedNode()
    {
        var sidebar = _fixtures.CreateSidebar();
        await sidebar.ReloadAsync();

        Assert.That(sidebar.NewFolderCommand.CanExecute(null), Is.False);
        Assert.That(sidebar.NewRequestCommand.CanExecute(null), Is.False);

        sidebar.SelectedNode = sidebar.Roots[0];

        Assert.That(sidebar.NewFolderCommand.CanExecute(null), Is.True);
        Assert.That(sidebar.NewRequestCommand.CanExecute(null), Is.True);
    }

    [Test]
    public async Task NewFolderCommandCreatesInTheSelectedNode()
    {
        var sidebar = _fixtures.CreateSidebar();
        await sidebar.ReloadAsync();
        var collection = sidebar.Roots[0];
        sidebar.SelectedNode = collection;
        A.CallTo(() => _fixtures.Folders.CreateAsync(A<Guid>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<Guid>.Success(Guid.NewGuid())));

        await sidebar.CreateFolderAsync(sidebar.SelectedNode);

        A.CallTo(() => _fixtures.Folders.CreateAsync(collection.DropFolderId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task OpenRequestRaisesRequestOpenedWithRequestId()
    {
        var sidebar = _fixtures.CreateSidebar();
        await sidebar.ReloadAsync();
        Guid? opened = null;
        sidebar.RequestOpenRequested += requestId => opened = requestId;

        sidebar.OpenRequest(_fixtures.Login.Id);

        Assert.That(opened, Is.EqualTo(_fixtures.Login.Id));
    }

    [Test]
    public async Task MoveRequestOntoFolderMovesItIntoThatFolder()
    {
        var sidebar = _fixtures.CreateSidebar();
        await sidebar.ReloadAsync();
        var source = sidebar.FindRequest(_fixtures.Login.Id)!;
        var target = sidebar.Roots[0].Children.OfType<FolderNodeViewModel>().Single();
        A.CallTo(() => _fixtures.Tree.MoveAsync(A<Guid>._, A<Guid>._, A<Guid?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Success()));

        await sidebar.MoveAsync(source, target);

        A.CallTo(() => _fixtures.Tree.MoveAsync(_fixtures.Login.Id, target.Id, null, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task MoveCollectionIsRejectedWithStatus()
    {
        var sidebar = _fixtures.CreateSidebar();
        await sidebar.ReloadAsync();
        var collection = sidebar.Roots[0];
        var target = sidebar.Roots[0].Children.OfType<FolderNodeViewModel>().Single();

        await sidebar.MoveAsync(collection, target);

        Assert.That(sidebar.StatusMessage, Is.EqualTo("Collections cannot be moved."));
        A.CallTo(() => _fixtures.Tree.MoveAsync(A<Guid>._, A<Guid>._, A<Guid?>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
