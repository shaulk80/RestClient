using FakeItEasy;
using RestDraft.Application.Abstractions;
using RestDraft.Application.Collections;
using RestDraft.Domain.Collections;
using RestDraft.Domain.Results;
using RestDraft.UI.Services;
using RestDraft.UI.ViewModels;

namespace RestDraft.UI.Tests.ViewModels;

internal sealed class TreeFixtures
{
    public TreeFixtures()
    {
        Login = new RequestDefinition { Id = Guid.NewGuid(), Name = "Login", Url = "https://example.com/login" };
        Users = new Folder(Guid.NewGuid(), "Users", [], [Login]);
        Root = new Folder(Guid.NewGuid(), string.Empty, [Users], []);
        Shop = new Collection(Guid.NewGuid(), "Shop", Root);
        Collections = A.Fake<ICollectionService>();
        Folders = A.Fake<IFolderService>();
        Requests = A.Fake<IRequestService>();
        Tree = A.Fake<ICollectionTreeService>();
        Confirmations = A.Fake<IConfirmationDialogs>();
        A.CallTo(() => Confirmations.ConfirmAsync(A<string>._, A<CancellationToken>._)).Returns(Task.FromResult(true));
        SetCollections([Shop]);
    }

    public RequestDefinition Login { get; }

    public Folder Users { get; }

    public Folder Root { get; }

    public Collection Shop { get; }

    public ICollectionService Collections { get; }

    public IFolderService Folders { get; }

    public IRequestService Requests { get; }

    public ICollectionTreeService Tree { get; }

    public IConfirmationDialogs Confirmations { get; }

    public static Folder CreateFolder(string name, IReadOnlyList<Folder>? folders = null)
    {
        return new Folder(Guid.NewGuid(), name, folders ?? [], []);
    }

    public CollectionsTreeViewModel CreateSidebar()
    {
        return new CollectionsTreeViewModel(Collections, Folders, Requests, Tree, Confirmations);
    }

    public void SetCollections(IReadOnlyList<Collection> collections)
    {
        A.CallTo(() => Collections.ListAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<IReadOnlyList<Collection>>.Success(collections)));
    }
}
