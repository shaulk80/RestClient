using RestClient.Domain.Collections;

namespace RestClient.UI.ViewModels;

public sealed class CollectionNodeViewModel : TreeNodeViewModel
{
    private readonly Guid _rootFolderId;

    public CollectionNodeViewModel(CollectionsTreeViewModel owner, Collection collection)
        : base(owner, collection.Id, collection.Name)
    {
        _rootFolderId = collection.Root.Id;
        Children = BuildChildren(owner, collection.Root);
    }

    public override bool CanContainChildren => true;

    public override Guid DropFolderId => _rootFolderId;

    public override Guid? DropBeforeId => null;
}
