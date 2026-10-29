using NUnit.Framework;
using RestDraft.Application.Collections;
using RestDraft.Application.Workspaces;
using RestDraft.Domain.Collections;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Tests.Collections;

[TestFixture]
public class CollectionServiceTests
{
    private static readonly string[] OrdersOnly = ["Orders"];
    private static readonly string[] KeptOnly = ["Kept"];
    private static readonly string[] OneOnly = ["One"];

    private CollectionStore _store = null!;
    private Workspace<Collection> _workspace = null!;
    private CollectionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _store = new CollectionStore([]);
        _workspace = CollectionRepositoryFake.CreateWorkspace(_store);
        _service = new CollectionService(_workspace, Fixtures.CreateIdGenerator());
    }

    [TearDown]
    public void TearDown()
    {
        _workspace.Dispose();
    }

    [Test]
    public async Task CreateAddsCollectionWithTrimmedName()
    {
        var result = await _service.CreateAsync("  Orders  ", CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(_store.Collections.Select(collection => collection.Name), Is.EqualTo(OrdersOnly));
        Assert.That(_store.Collections[0].Id, Is.EqualTo(result.Value));
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task CreateRejectsBlankName(string name)
    {
        var result = await _service.CreateAsync(name, CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
        Assert.That(_store.Collections, Is.Empty);
    }

    [Test]
    public async Task RenameChangesCollectionName()
    {
        var collection = Fixtures.Collection("Old", Fixtures.Folder(string.Empty));
        _store.Collections = [collection];

        var result = await _service.RenameAsync(collection.Id, "New", CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(_store.Collections[0].Name, Is.EqualTo("New"));
    }

    [Test]
    public async Task RenameReturnsNotFoundForMissingCollection()
    {
        var result = await _service.RenameAsync(Guid.NewGuid(), "New", CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.NotFound));
    }

    [Test]
    public async Task DeleteRemovesCollectionAndItsTree()
    {
        var collection = Fixtures.Collection("Doomed", Fixtures.Folder(string.Empty, [Fixtures.Folder("Inner")]));
        var kept = Fixtures.Collection("Kept", Fixtures.Folder(string.Empty));
        _store.Collections = [collection, kept];

        var result = await _service.DeleteAsync(collection.Id, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(_store.Collections.Select(item => item.Name), Is.EqualTo(KeptOnly));
    }

    [Test]
    public async Task DeleteReturnsNotFoundForMissingCollection()
    {
        var result = await _service.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.NotFound));
    }

    [Test]
    public async Task ListReturnsStoredCollections()
    {
        _store.Collections = [Fixtures.Collection("One", Fixtures.Folder(string.Empty))];

        var result = await _service.ListAsync(CancellationToken.None);

        Assert.That(result.Value?.Select(collection => collection.Name), Is.EqualTo(OneOnly));
    }
}
