namespace RestDraft.UI.Services;

public interface IConfirmationDialogs
{
    // True when the user confirms. Cancelling, or having no window to attach to, gives false.
    Task<bool> ConfirmAsync(string message, CancellationToken cancellationToken);
}
