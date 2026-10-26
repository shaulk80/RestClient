namespace RestClient.UI.Services;

// Each method returns true when the secrets end up unlocked, and false when the user cancels.
public interface ISecretDialogs
{
    Task<bool> ShowUnlockAsync(CancellationToken cancellationToken);

    Task<bool> ShowSetUpAsync(CancellationToken cancellationToken);
}
