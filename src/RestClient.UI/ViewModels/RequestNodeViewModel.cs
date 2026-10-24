using RestClient.Domain.Collections;

namespace RestClient.UI.ViewModels;

public sealed class RequestNodeViewModel : TreeNodeViewModel
{
    private readonly Guid _parentFolderId;

    public RequestNodeViewModel(CollectionsTreeViewModel owner, RequestDefinition request, Guid parentFolderId)
        : base(owner, request.Id, request.Name)
    {
        _parentFolderId = parentFolderId;
    }

    public override bool CanContainChildren => false;

    public override bool IsRequest => true;

    public override Guid DropFolderId => _parentFolderId;

    public override Guid? DropBeforeId => Id;
}
