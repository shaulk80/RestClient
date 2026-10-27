using NUnit.Framework;
using RestClient.Application.Collections;
using RestClient.Application.Workspaces;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;

namespace RestClient.Application.Tests.Collections;

[TestFixture]
public class CollectionTreeServiceTests
{
    private static readonly string[] SecondThenFirst = ["Second", "First"];
    private static readonly string[] SiblingSourceTarget = ["Sibling", "Source", "Target"];

    private CollectionStore _store = null!;
    private Workspace<Collection> _workspace = null!;
    private CollectionTreeService _service = null!;
    private Folder _root = null!;
    private Folder _source = null!;
    private Folder _target = null!;
    private RequestDefinition _first = null!;
    private RequestDefinition _second = null!;

    [SetUp]
    public void SetUp()
    {
        _first = Fixtures.Request("First");
        _second = Fixtures.Request("Second");
        _source = Fixtures.Folder("Source", [], [_first, _second]);
        _target = Fixtures.Folder("Target");
        _root = Fixtures.Folder(string.Empty, [_source, _target]);
        _store = new CollectionStore([Fixtures.Collection("Shop", _root)]);
        _workspace = CollectionRepositoryFake.CreateWorkspace(_store);
        _service = new CollectionTreeService(_workspace);
    }

    [TearDown]
    public void TearDown()
    {
        _workspace.Dispose();
    }

    [Test]
    public async Task MoveRequestIntoAnotherFolder()
    {
        var result = await _service.MoveAsync(_first.Id, _target.Id, null, CancellationToken.None);

        var root = _store.Collections[0].Root;
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(FolderTree.FindRequestParent(root, _first.Id)?.Id, Is.EqualTo(_target.Id));
        Assert.That(FolderTree.FindRequestParent(root, _second.Id)?.Id, Is.EqualTo(_source.Id));
    }

    [Test]
    public async Task ReorderPlacesRequestBeforeSiblingWithoutDuplicating()
    {
        var result = await _service.MoveAsync(_second.Id, _source.Id, _first.Id, CancellationToken.None);

        var names = FolderTree.FindFolder(_store.Collections[0].Root, _source.Id)!.Requests.Select(request => request.Name);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(names, Is.EqualTo(SecondThenFirst));
    }

    [Test]
    public async Task MoveFolderBeforeSiblingFolder()
    {
        var sibling = Fixtures.Folder("Sibling");
        _root = _root with { Folders = [_source, _target, sibling] };
        _store.Collections = [Fixtures.Collection("Shop", _root)];

        var result = await _service.MoveAsync(sibling.Id, _root.Id, _source.Id, CancellationToken.None);

        var names = FolderTree.FindFolder(_store.Collections[0].Root, _root.Id)!.Folders.Select(folder => folder.Name);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(names, Is.EqualTo(SiblingSourceTarget));
    }

    [Test]
    public async Task MoveFolderIntoItsOwnDescendantIsRejected()
    {
        var child = Fixtures.Folder("Child");
        var parent = Fixtures.Folder("Parent", [child]);
        _root = _root with { Folders = [parent] };
        _store.Collections = [Fixtures.Collection("Shop", _root)];

        var result = await _service.MoveAsync(parent.Id, child.Id, null, CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
        Assert.That(FolderTree.FindFolderParent(_store.Collections[0].Root, child.Id)?.Id, Is.EqualTo(parent.Id));
    }

    [Test]
    public async Task MoveFolderIntoItselfIsRejected()
    {
        var result = await _service.MoveAsync(_source.Id, _source.Id, null, CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
    }

    [Test]
    public async Task MoveFolderWithSameNameAsTargetChildIsRejectedAndTreeUnchanged()
    {
        _target = _target with { Requests = [Fixtures.Request("Source")] };
        _root = _root with { Folders = [_source, _target] };
        _store.Collections = [Fixtures.Collection("Shop", _root)];

        var result = await _service.MoveAsync(_source.Id, _target.Id, null, CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
        Assert.That(FolderTree.FindFolderParent(_store.Collections[0].Root, _source.Id)?.Id, Is.EqualTo(_root.Id));
    }

    [Test]
    public async Task MoveToMissingTargetReturnsNotFound()
    {
        var result = await _service.MoveAsync(_first.Id, Guid.NewGuid(), null, CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.NotFound));
    }

    [Test]
    public async Task MoveUnknownItemReturnsNotFound()
    {
        var result = await _service.MoveAsync(Guid.NewGuid(), _target.Id, null, CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.NotFound));
    }

    [Test]
    public async Task MoveCollectionRootIsRejected()
    {
        var result = await _service.MoveAsync(_root.Id, _target.Id, null, CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
    }
}
