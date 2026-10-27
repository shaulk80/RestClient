using FakeItEasy;
using NUnit.Framework;
using RestClient.Application.Abstractions;
using RestClient.Application.Collections;
using RestClient.Application.Tabs;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;

namespace RestClient.Application.Tests.Tabs;

[TestFixture]
public class TabServiceTests
{
    private static readonly bool[] ExpectedSelection = [false, true, false];
    private static readonly string[] ExpectedNames = ["First", "Second"];

    private IOpenTabsStore _store = null!;
    private IRequestService _requests = null!;
    private TabService _service = null!;
    private IReadOnlyList<OpenTab>? _saved;

    [SetUp]
    public void SetUp()
    {
        _saved = null;
        _store = A.Fake<IOpenTabsStore>();
        _requests = A.Fake<IRequestService>();
        A.CallTo(() => _store.SaveAsync(A<IReadOnlyList<OpenTab>>._, A<CancellationToken>._))
            .Invokes((IReadOnlyList<OpenTab> tabs, CancellationToken _) => _saved = tabs)
            .Returns(Task.FromResult(Result.Success()));
        _service = new TabService(_store, _requests);
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
    }

    [Test]
    public async Task SaveStoresDraftOnlyForDirtyOrUnlinkedTabs()
    {
        var clean = Request("Clean");
        var dirty = Request("Dirty");
        var unsaved = Request("Unsaved");

        await _service.SaveAsync(
            [
                new TabSnapshot(clean, IsLinked: true, IsDirty: false, IsSelected: false),
                new TabSnapshot(dirty, IsLinked: true, IsDirty: true, IsSelected: true),
                new TabSnapshot(unsaved, IsLinked: false, IsDirty: true, IsSelected: false),
            ],
            CancellationToken.None);

        Assert.That(_saved!.Select(tab => tab.Draft), Is.EqualTo(new RequestDefinition?[] { null, dirty, unsaved }));
        Assert.That(_saved!.Select(tab => tab.IsSelected), Is.EqualTo(ExpectedSelection));
        Assert.That(_saved!.Select(tab => tab.RequestId), Is.EqualTo(new[] { clean.Id, dirty.Id, unsaved.Id }));
    }

    [Test]
    public async Task SaveReturnsStoreFailure()
    {
        A.CallTo(() => _store.SaveAsync(A<IReadOnlyList<OpenTab>>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Failure(new Error(ErrorCategory.Storage, "Disk full."))));

        var result = await _service.SaveAsync([], CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Storage));
    }

    [Test]
    public async Task RestoreOpensCleanLinkedTabFromTheCollection()
    {
        var saved = Request("Saved");
        GivenStored(new OpenTab(saved.Id, IsLinked: true, IsSelected: true, Draft: null));
        GivenRequestExists(saved);

        var result = await _service.RestoreAsync(CancellationToken.None);

        Assert.That(result.Value, Has.Count.EqualTo(1));
        Assert.That(result.Value![0], Is.EqualTo(new RestoredTab(saved, IsLinked: true, IsDirty: false, IsSelected: true)));
    }

    [Test]
    public async Task RestoreKeepsDraftOfLinkedTabAndMarksItDirty()
    {
        var saved = Request("Saved");
        var draft = saved with { Url = "https://edited.example.com" };
        GivenStored(new OpenTab(saved.Id, IsLinked: true, IsSelected: false, Draft: draft));
        GivenRequestExists(saved with { Name = "Renamed" });

        var result = await _service.RestoreAsync(CancellationToken.None);

        var restored = result.Value![0];
        Assert.That(restored.IsDirty, Is.True);
        Assert.That(restored.IsLinked, Is.True);
        Assert.That(restored.Definition.Url, Is.EqualTo("https://edited.example.com"));
        Assert.That(restored.Definition.Name, Is.EqualTo("Renamed"));
    }

    [Test]
    public async Task RestoreOpensUnsavedTabFromItsDraft()
    {
        var unsaved = Request("Unsaved");
        GivenStored(new OpenTab(unsaved.Id, IsLinked: false, IsSelected: false, Draft: unsaved));

        var result = await _service.RestoreAsync(CancellationToken.None);

        Assert.That(result.Value![0], Is.EqualTo(new RestoredTab(unsaved, IsLinked: false, IsDirty: true, IsSelected: false)));
    }

    [Test]
    public async Task RestoreTurnsDraftOfDeletedRequestIntoUnsavedTab()
    {
        var deleted = Request("Deleted");
        GivenStored(new OpenTab(deleted.Id, IsLinked: true, IsSelected: false, Draft: deleted));
        GivenRequestIsMissing();

        var result = await _service.RestoreAsync(CancellationToken.None);

        Assert.That(result.Value![0], Is.EqualTo(new RestoredTab(deleted, IsLinked: false, IsDirty: true, IsSelected: false)));
    }

    [Test]
    public async Task RestoreDropsCleanTabOfDeletedRequest()
    {
        GivenStored(new OpenTab(Guid.NewGuid(), IsLinked: true, IsSelected: false, Draft: null));
        GivenRequestIsMissing();

        var result = await _service.RestoreAsync(CancellationToken.None);

        Assert.That(result.Value, Is.Empty);
    }

    [Test]
    public async Task RestoreDropsUnlinkedTabWithoutDraft()
    {
        GivenStored(new OpenTab(Guid.NewGuid(), IsLinked: false, IsSelected: false, Draft: null));

        var result = await _service.RestoreAsync(CancellationToken.None);

        Assert.That(result.Value, Is.Empty);
    }

    [Test]
    public async Task RestoreKeepsTheStoredOrder()
    {
        var first = Request("First");
        var second = Request("Second");
        GivenStored(
            new OpenTab(first.Id, IsLinked: false, IsSelected: false, Draft: first),
            new OpenTab(second.Id, IsLinked: false, IsSelected: true, Draft: second));

        var result = await _service.RestoreAsync(CancellationToken.None);

        Assert.That(result.Value!.Select(tab => tab.Definition.Name), Is.EqualTo(ExpectedNames));
    }

    [Test]
    public async Task RestoreReturnsStoreFailure()
    {
        A.CallTo(() => _store.LoadAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<IReadOnlyList<OpenTab>>.Failure(new Error(ErrorCategory.Storage, "Unreadable."))));

        var result = await _service.RestoreAsync(CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Storage));
    }

    private static RequestDefinition Request(string name)
    {
        return new RequestDefinition { Id = Guid.NewGuid(), Name = name, Url = "https://example.com" };
    }

    private void GivenStored(params OpenTab[] tabs)
    {
        A.CallTo(() => _store.LoadAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<IReadOnlyList<OpenTab>>.Success(tabs)));
    }

    private void GivenRequestExists(RequestDefinition request)
    {
        A.CallTo(() => _requests.GetAsync(request.Id, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<RequestDefinition>.Success(request)));
    }

    private void GivenRequestIsMissing()
    {
        A.CallTo(() => _requests.GetAsync(A<Guid>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<RequestDefinition>.Failure(ResultErrors.NotFound("The request"))));
    }
}
