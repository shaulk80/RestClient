using FakeItEasy;
using RestClient.Application.Abstractions;
using RestClient.Application.Collections;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;
using RestClient.UI.ViewModels;

namespace RestClient.UI.Tests.ViewModels;

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

    public CollectionsTreeViewModel CreateSidebar()
    {
        return new CollectionsTreeViewModel(Collections, Folders, Requests, Tree);
    }

    public void SetCollections(IReadOnlyList<Collection> collections)
    {
        A.CallTo(() => Collections.ListAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<IReadOnlyList<Collection>>.Success(collections)));
    }
}
