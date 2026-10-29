using NUnit.Framework;
using RestDraft.Application.Collections;
using RestDraft.Application.Workspaces;
using RestDraft.Domain.Collections;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Tests.Collections;

[TestFixture]
public class FolderServiceTests
{
    private CollectionStore _store = null!;
    private Workspace<Collection> _workspace = null!;
    private FolderService _service = null!;
    private Folder _root = null!;
    private Folder _users = null!;

    [SetUp]
    public void SetUp()
    {
        _users = Fixtures.Folder("Users");
        _root = Fixtures.Folder(string.Empty, [_users]);
        _store = new CollectionStore([Fixtures.Collection("Shop", _root)]);
        _workspace = CollectionRepositoryFake.CreateWorkspace(_store);
        _service = new FolderService(_workspace, Fixtures.CreateIdGenerator());
    }

    [TearDown]
    public void TearDown()
    {
        _workspace.Dispose();
    }

    [Test]
    public async Task CreateAddsFolderNamedNewFolderUnderParent()
    {
        var result = await _service.CreateAsync(_root.Id, CancellationToken.None);

        var created = FolderTree.FindFolder(_store.Collections[0].Root, result.Value);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(created?.Name, Is.EqualTo("New folder"));
        Assert.That(FolderTree.FindFolderParent(_store.Collections[0].Root, result.Value)?.Id, Is.EqualTo(_root.Id));
    }

    [Test]
    public async Task CreateGivesSecondNewFolderASuffix()
    {
        await _service.CreateAsync(_root.Id, CancellationToken.None);

        var second = await _service.CreateAsync(_root.Id, CancellationToken.None);

        Assert.That(FolderTree.FindFolder(_store.Collections[0].Root, second.Value)?.Name, Is.EqualTo("New folder 2"));
    }

    [Test]
    public async Task CreateReturnsNotFoundForMissingParent()
    {
        var result = await _service.CreateAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.NotFound));
    }

    [Test]
    public async Task RenameChangesFolderName()
    {
        var result = await _service.RenameAsync(_users.Id, "Accounts", CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(FolderTree.FindFolder(_store.Collections[0].Root, _users.Id)?.Name, Is.EqualTo("Accounts"));
    }

    [Test]
    public async Task RenameRejectsSiblingName()
    {
        var sibling = Fixtures.Folder("Orders");
        _root = _root with { Folders = [_users, sibling] };
        _store.Collections = [Fixtures.Collection("Shop", _root)];

        var result = await _service.RenameAsync(_users.Id, "orders", CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
        Assert.That(FolderTree.FindFolder(_store.Collections[0].Root, _users.Id)?.Name, Is.EqualTo("Users"));
    }

    [Test]
    public async Task RenameRejectsCollectionRoot()
    {
        var result = await _service.RenameAsync(_root.Id, "Anything", CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
    }

    [Test]
    public async Task RenameReturnsNotFoundForMissingFolder()
    {
        var result = await _service.RenameAsync(Guid.NewGuid(), "Anything", CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.NotFound));
    }

    [Test]
    public async Task DeleteRemovesFolderWithItsDescendants()
    {
        var child = Fixtures.Folder("Inner");
        var parent = Fixtures.Folder("Outer", [child]);
        _root = _root with { Folders = [parent] };
        _store.Collections = [Fixtures.Collection("Shop", _root)];

        var result = await _service.DeleteAsync(parent.Id, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(FolderTree.FindFolder(_store.Collections[0].Root, child.Id), Is.Null);
    }

    [Test]
    public async Task DeleteRejectsCollectionRoot()
    {
        var result = await _service.DeleteAsync(_root.Id, CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
        Assert.That(_store.Collections[0].Root.Id, Is.EqualTo(_root.Id));
    }

    [Test]
    public async Task DeleteReturnsNotFoundForMissingFolder()
    {
        var result = await _service.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.NotFound));
    }
}
