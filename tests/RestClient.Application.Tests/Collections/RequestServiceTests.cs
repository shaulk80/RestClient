using NUnit.Framework;
using RestClient.Application.Collections;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;

namespace RestClient.Application.Tests.Collections;

[TestFixture]
public class RequestServiceTests
{
    private static readonly string[] LoginCopyLast = ["Login", "Login copy", "Last"];

    private CollectionStore _store = null!;
    private CollectionWorkspace _workspace = null!;
    private RequestService _service = null!;
    private Folder _root = null!;
    private RequestDefinition _login = null!;

    [SetUp]
    public void SetUp()
    {
        _login = Fixtures.Request("Login");
        _root = Fixtures.Folder(string.Empty, [], [_login]);
        _store = new CollectionStore([Fixtures.Collection("Auth", _root)]);
        _workspace = new CollectionWorkspace(CollectionRepositoryFake.Create(_store));
        _service = new RequestService(_workspace, Fixtures.CreateIdGenerator());
    }

    [TearDown]
    public void TearDown()
    {
        _workspace.Dispose();
    }

    [Test]
    public async Task CreateAddsNewRequestToFolder()
    {
        var result = await _service.CreateAsync(_root.Id, CancellationToken.None);

        var created = FolderTree.FindRequest(_store.Collections[0].Root, result.Value);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(created?.Name, Is.EqualTo("New request"));
    }

    [Test]
    public async Task GetReturnsStoredRequest()
    {
        var result = await _service.GetAsync(_login.Id, CancellationToken.None);

        Assert.That(result.Value?.Name, Is.EqualTo("Login"));
    }

    [Test]
    public async Task GetReturnsNotFoundForMissingRequest()
    {
        var result = await _service.GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.NotFound));
    }

    [Test]
    public async Task UpdateAppliesChangeToStoredRequest()
    {
        var result = await _service.UpdateAsync(_login.Id, request => request with { Url = "https://example.com/login" }, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(FolderTree.FindRequest(_store.Collections[0].Root, _login.Id)?.Url, Is.EqualTo("https://example.com/login"));
    }

    [Test]
    public async Task UpdateReturnsNotFoundForMissingRequest()
    {
        var result = await _service.UpdateAsync(Guid.NewGuid(), request => request, CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.NotFound));
    }

    [Test]
    public async Task RenameRejectsSiblingName()
    {
        var other = Fixtures.Request("Logout");
        _root = _root with { Requests = [_login, other] };
        _store.Collections = [Fixtures.Collection("Auth", _root)];

        var result = await _service.RenameAsync(_login.Id, "Logout", CancellationToken.None);

        Assert.That(result.Error?.Category, Is.EqualTo(ErrorCategory.Validation));
    }

    [Test]
    public async Task RenameKeepsNameWhenRenamedToItself()
    {
        var result = await _service.RenameAsync(_login.Id, "Login", CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public async Task DuplicateInsertsCopyWithNewIdRightAfterOriginal()
    {
        var last = Fixtures.Request("Last");
        _root = _root with { Requests = [_login, last] };
        _store.Collections = [Fixtures.Collection("Auth", _root)];

        var result = await _service.DuplicateAsync(_login.Id, CancellationToken.None);

        var names = FolderTree.FindFolder(_store.Collections[0].Root, _root.Id)!.Requests.Select(request => request.Name);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(names, Is.EqualTo(LoginCopyLast));
        Assert.That(result.Value, Is.Not.EqualTo(_login.Id));
    }

    [Test]
    public async Task DeleteRemovesRequest()
    {
        var result = await _service.DeleteAsync(_login.Id, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(FolderTree.FindRequest(_store.Collections[0].Root, _login.Id), Is.Null);
    }
}
