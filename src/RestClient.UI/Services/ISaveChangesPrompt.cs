namespace RestClient.UI.Services;

public interface ISaveChangesPrompt
{
    // Closing the dialog, or having no window to attach to, gives Cancel.
    Task<SaveChangesChoice> AskAsync(string requestName, CancellationToken cancellationToken);
}
