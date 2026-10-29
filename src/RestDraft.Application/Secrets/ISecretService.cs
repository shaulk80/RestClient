using RestDraft.Domain.Results;

namespace RestDraft.Application.Secrets;

// Holds the derived key in memory while unlocked. The master password itself is never stored.
public interface ISecretService
{
    bool IsUnlocked { get; }

    event EventHandler? StateChanged;

    Task<Result<bool>> IsSetUpAsync(CancellationToken cancellationToken);

    Task<Result> SetUpAsync(string masterPassword, CancellationToken cancellationToken);

    Task<Result> UnlockAsync(string masterPassword, CancellationToken cancellationToken);

    void Lock();

    Result<string> Encrypt(string plainText);

    Result<string> Decrypt(string cipherText);
}
