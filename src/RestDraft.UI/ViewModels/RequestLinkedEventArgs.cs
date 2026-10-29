namespace RestDraft.UI.ViewModels;

// Raised when an unsaved tab is saved into a collection. ExpandId is the tree node to expand.
public sealed class RequestLinkedEventArgs : EventArgs
{
    public RequestLinkedEventArgs(Guid requestId, Guid expandId)
    {
        RequestId = requestId;
        ExpandId = expandId;
    }

    public Guid RequestId { get; }

    public Guid ExpandId { get; }
}
