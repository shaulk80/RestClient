using RestClient.Domain.Collections;

namespace RestClient.UI.ViewModels;

public sealed class FolderNodeViewModel : TreeNodeViewModel
{
    public FolderNodeViewModel(CollectionsTreeViewModel owner, Folder folder)
        : base(owner, folder.Id, folder.Name)
    {
        Children = BuildChildren(owner, folder);
    }

    public override bool CanContainChildren => true;

    public override Guid DropFolderId => Id;

    public override Guid? DropBeforeId => null;
}
