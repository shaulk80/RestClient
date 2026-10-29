using RestDraft.Application.Secrets;
using RestDraft.Domain.Results;

namespace RestDraft.UI.Services;

public sealed class SecretPrompt : ISecretPrompt
{
    private readonly ISecretService _secrets;
    private readonly ISecretDialogs _dialogs;

    public SecretPrompt(ISecretService secrets, ISecretDialogs dialogs)
    {
        _secrets = secrets;
        _dialogs = dialogs;
    }

    public async Task<Result<bool>> EnsureUnlockedAsync(CancellationToken cancellationToken)
    {
        if (_secrets.IsUnlocked)
        {
            return Result<bool>.Success(true);
        }

        var setUp = await _secrets.IsSetUpAsync(cancellationToken);
        if (!setUp.IsSuccess)
        {
            return Result<bool>.Failure(setUp.Error!);
        }

        var unlocked = setUp.Value
            ? await _dialogs.ShowUnlockAsync(cancellationToken)
            : await _dialogs.ShowSetUpAsync(cancellationToken);
        return Result<bool>.Success(unlocked);
    }
}
