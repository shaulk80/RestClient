using RestClient.Domain.Results;

namespace RestClient.UI.Services;

public interface ISecretPrompt
{
    // Success with true means the secrets are unlocked. Shows the setup or unlock dialog when they are not.
    Task<Result<bool>> EnsureUnlockedAsync(CancellationToken cancellationToken);
}
