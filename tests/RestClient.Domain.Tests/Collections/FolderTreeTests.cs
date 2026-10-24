using NUnit.Framework;
using RestClient.Domain.Collections;

namespace RestClient.Domain.Tests.Collections;

[TestFixture]
public class FolderTreeTests
{
    private static readonly Guid RootId = Guid.NewGuid();
    private static readonly Guid AlphaId = Guid.NewGuid();
    private static readonly Guid BetaId = Guid.NewGuid();
    private static readonly Guid GammaId = Guid.NewGuid();
    private static readonly Guid FirstRequestId = Guid.NewGuid();
    private static readonly Guid SecondRequestId = Guid.NewGuid();
    private static readonly Guid RootRequestId = Guid.NewGuid();
    private static readonly string[] AlphaOnly = ["Alpha"];
    private static readonly string[] FolderOrderInsertedBeforeBeta = ["Alpha", "Delta", "Beta"];
    private static readonly string[] FolderOrderInsertedAtEnd = ["Alpha", "Beta", "Delta"];
    private static readonly string[] RequestOrderAppended = ["first", "second", "added"];
    private static readonly string[] RequestOrderCopiedAfterFirst = ["first", "copy", "second"];
    private static readonly string[] RequestOrderCopiedAfterSecond = ["first", "second", "copy"];

    [Test]
    public void FindFolderReturnsNestedFolder()
    {
        var found = FolderTree.FindFolder(CreateRoot(), GammaId);

        Assert.That(found?.Name, Is.EqualTo("Gamma"));
    }

    [Test]
    public void FindFolderReturnsNullForUnknownId()
    {
        var found = FolderTree.FindFolder(CreateRoot(), Guid.NewGuid());

        Assert.That(found, Is.Null);
    }

    [TestCase("Gamma", "Beta")]
    [TestCase("Alpha", "Root")]
    public void FindFolderParentReturnsContainingFolder(string childName, string parentName)
    {
        var parent = FolderTree.FindFolderParent(CreateRoot(), IdOf(childName));

        Assert.That(parent?.Name, Is.EqualTo(parentName));
    }

    [Test]
    public void FindFolderParentReturnsNullForRoot()
    {
        var parent = FolderTree.FindFolderParent(CreateRoot(), RootId);

        Assert.That(parent, Is.Null);
    }

    [Test]
    public void FindRequestReturnsRequestInNestedFolder()
    {
        var request = FolderTree.FindRequest(CreateRoot(), FirstRequestId);

        Assert.That(request?.Name, Is.EqualTo("first"));
    }

    [TestCase("first", "Alpha")]
    [TestCase("root request", "Root")]
    public void FindRequestParentReturnsFolderThatHoldsRequest(string requestName, string parentName)
    {
        var parent = FolderTree.FindRequestParent(CreateRoot(), IdOf(requestName));

        Assert.That(parent?.Name, Is.EqualTo(parentName));
    }

    [Test]
    public void FindRequestReturnsNullForUnknownId()
    {
        var request = FolderTree.FindRequest(CreateRoot(), Guid.NewGuid());

        Assert.That(request, Is.Null);
    }

    [Test]
    public void RemoveFolderRemovesFolderWithItsDescendants()
    {
        var root = FolderTree.RemoveFolder(CreateRoot(), BetaId);

        Assert.That(FolderTree.FindFolder(root, GammaId), Is.Null);
        Assert.That(root.Folders.Select(folder => folder.Name), Is.EqualTo(AlphaOnly));
    }

    [Test]
    public void RemoveFolderLeavesTreeUnchangedForRoot()
    {
        var root = FolderTree.RemoveFolder(CreateRoot(), RootId);

        Assert.That(root.Folders.Count, Is.EqualTo(2));
    }

    [Test]
    public void RemoveRequestRemovesOnlyThatRequest()
    {
        var root = FolderTree.RemoveRequest(CreateRoot(), FirstRequestId);

        Assert.That(FolderTree.FindRequest(root, FirstRequestId), Is.Null);
        Assert.That(FolderTree.FindRequest(root, SecondRequestId), Is.Not.Null);
    }

    [Test]
    public void UpdateRequestAppliesChangeToThatRequest()
    {
        var root = FolderTree.UpdateRequest(CreateRoot(), FirstRequestId, request => request with { Name = "renamed" });

        Assert.That(FolderTree.FindRequest(root, FirstRequestId)?.Name, Is.EqualTo("renamed"));
    }

    [Test]
    public void InsertFolderPlacesFolderBeforeSibling()
    {
        var child = new Folder(Guid.NewGuid(), "Delta", [], []);

        var root = FolderTree.InsertFolder(CreateRoot(), RootId, child, BetaId);

        Assert.That(root.Folders.Select(folder => folder.Name), Is.EqualTo(FolderOrderInsertedBeforeBeta));
    }

    [Test]
    public void InsertFolderAppendsWhenBeforeIdIsNull()
    {
        var child = new Folder(Guid.NewGuid(), "Delta", [], []);

        var root = FolderTree.InsertFolder(CreateRoot(), RootId, child, null);

        Assert.That(root.Folders.Select(folder => folder.Name), Is.EqualTo(FolderOrderInsertedAtEnd));
    }

    [Test]
    public void InsertRequestAppendsWhenBeforeIdIsUnknown()
    {
        var request = new RequestDefinition { Id = Guid.NewGuid(), Name = "added" };

        var root = FolderTree.InsertRequest(CreateRoot(), AlphaId, request, Guid.NewGuid());

        Assert.That(FolderTree.FindFolder(root, AlphaId)?.Requests.Select(item => item.Name), Is.EqualTo(RequestOrderAppended));
    }

    [Test]
    public void InsertRequestAfterPlacesRequestAfterSibling()
    {
        var request = new RequestDefinition { Id = Guid.NewGuid(), Name = "copy" };

        var root = FolderTree.InsertRequestAfter(CreateRoot(), FirstRequestId, request);

        Assert.That(FolderTree.FindFolder(root, AlphaId)?.Requests.Select(item => item.Name), Is.EqualTo(RequestOrderCopiedAfterFirst));
    }

    [Test]
    public void InsertRequestAfterLastSiblingAppends()
    {
        var request = new RequestDefinition { Id = Guid.NewGuid(), Name = "copy" };

        var root = FolderTree.InsertRequestAfter(CreateRoot(), SecondRequestId, request);

        Assert.That(FolderTree.FindFolder(root, AlphaId)?.Requests.Select(item => item.Name), Is.EqualTo(RequestOrderCopiedAfterSecond));
    }

    [Test]
    public void InsertRequestAfterLeavesTreeUnchangedForUnknownSibling()
    {
        var request = new RequestDefinition { Id = Guid.NewGuid(), Name = "copy" };

        var root = FolderTree.InsertRequestAfter(CreateRoot(), Guid.NewGuid(), request);

        Assert.That(FolderTree.FindRequest(root, request.Id), Is.Null);
    }

    // Test cases carry names because attribute arguments must be constants, so ids are looked up here.
    private static Guid IdOf(string name)
    {
        return name switch
        {
            "Alpha" => AlphaId,
            "Beta" => BetaId,
            "Gamma" => GammaId,
            "first" => FirstRequestId,
            "root request" => RootRequestId,
            _ => Guid.Empty,
        };
    }

    private static Folder CreateRoot()
    {
        var gamma = new Folder(GammaId, "Gamma", [], []);
        var beta = new Folder(BetaId, "Beta", [gamma], []);
        var alpha = new Folder(AlphaId, "Alpha", [], [
            new RequestDefinition { Id = FirstRequestId, Name = "first" },
            new RequestDefinition { Id = SecondRequestId, Name = "second" },
        ]);
        return new Folder(RootId, "Root", [alpha, beta], [new RequestDefinition { Id = RootRequestId, Name = "root request" }]);
    }
}
