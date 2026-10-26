using System.Globalization;
using System.Security.Cryptography;
using RestClient.Application.Abstractions;
using RestClient.Domain.Results;

namespace RestClient.Application.Secrets;

public sealed class SecretService : ISecretService
{
    // The iteration count follows the plan's proposed default. It is stored with each setup, so it can change later.
    public const int DefaultIterations = 600_000;

    private const int SaltLength = 16;
    private const string SaltKey = "secrets.salt";
    private const string IterationsKey = "secrets.iterations";
    private const string VerifierKey = "secrets.verifier";
    private const string VerifierText = "restclient-verifier";

    private readonly ISettingsStore _settings;
    private readonly IKeyDeriver _deriver;
    private readonly ISecretCipher _cipher;
    private readonly IRandomBytes _random;
    private byte[]? _key;

    public SecretService(ISettingsStore settings, IKeyDeriver deriver, ISecretCipher cipher, IRandomBytes random)
    {
        _settings = settings;
        _deriver = deriver;
        _cipher = cipher;
        _random = random;
    }

    public bool IsUnlocked => _key is not null;

    public event EventHandler? StateChanged;

    public async Task<Result<bool>> IsSetUpAsync(CancellationToken cancellationToken)
    {
        var verifier = await _settings.GetAsync(VerifierKey, cancellationToken);
        return verifier.IsSuccess
            ? Result<bool>.Success(verifier.Value is not null)
            : Result<bool>.Failure(verifier.Error!);
    }

    public async Task<Result> SetUpAsync(string masterPassword, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(masterPassword))
        {
            return Result.Failure(ResultErrors.Validation("Enter a master password."));
        }

        var setUp = await IsSetUpAsync(cancellationToken);
        if (!setUp.IsSuccess)
        {
            return Result.Failure(setUp.Error!);
        }

        if (setUp.Value)
        {
            return Result.Failure(new Error(ErrorCategory.Conflict, "A master password is already set."));
        }

        var salt = _random.Generate(SaltLength);
        var key = _deriver.Derive(masterPassword, salt, DefaultIterations);
        var verifier = _cipher.Encrypt(VerifierText, key);
        if (!verifier.IsSuccess)
        {
            return Result.Failure(verifier.Error!);
        }

        var saved = await SaveSetupAsync(salt, verifier.Value!, cancellationToken);
        if (saved.IsSuccess)
        {
            SetKey(key);
        }

        return saved;
    }

    public async Task<Result> UnlockAsync(string masterPassword, CancellationToken cancellationToken)
    {
        var setup = await LoadSetupAsync(cancellationToken);
        if (!setup.IsSuccess)
        {
            return Result.Failure(setup.Error!);
        }

        if (setup.Value is not { } stored)
        {
            return Result.Failure(ResultErrors.Validation("Set a master password first."));
        }

        var key = _deriver.Derive(masterPassword, stored.Salt, stored.Iterations);
        var verified = _cipher.Decrypt(stored.Verifier, key);
        if (!verified.IsSuccess || verified.Value != VerifierText)
        {
            return Result.Failure(new Error(ErrorCategory.Security, "The master password is wrong."));
        }

        SetKey(key);
        return Result.Success();
    }

    public void Lock()
    {
        if (_key is null)
        {
            return;
        }

        Array.Clear(_key);
        _key = null;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public Result<string> Encrypt(string plainText)
    {
        return WithKey(key => _cipher.Encrypt(plainText, key));
    }

    public Result<string> Decrypt(string cipherText)
    {
        return WithKey(key => _cipher.Decrypt(cipherText, key));
    }

    private static Result<string> Locked()
    {
        return Result<string>.Failure(new Error(ErrorCategory.Security, "The secrets are locked. Unlock them to continue."));
    }

    private Result<string> WithKey(Func<byte[], Result<string>> operation)
    {
        return _key is { } key ? operation(key) : Locked();
    }

    private void SetKey(byte[] key)
    {
        if (_key is not null)
        {
            Array.Clear(_key);
        }

        _key = key;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    // The verifier is written last, because its presence is what marks the setup as complete.
    private async Task<Result> SaveSetupAsync(byte[] salt, string verifier, CancellationToken cancellationToken)
    {
        var saltSaved = await _settings.SetAsync(SaltKey, Convert.ToBase64String(salt), cancellationToken);
        if (!saltSaved.IsSuccess)
        {
            return saltSaved;
        }

        var iterationsSaved = await _settings.SetAsync(IterationsKey, DefaultIterations.ToString(CultureInfo.InvariantCulture), cancellationToken);
        if (!iterationsSaved.IsSuccess)
        {
            return iterationsSaved;
        }

        return await _settings.SetAsync(VerifierKey, verifier, cancellationToken);
    }

    private async Task<Result<SecretSetup?>> LoadSetupAsync(CancellationToken cancellationToken)
    {
        var verifier = await _settings.GetAsync(VerifierKey, cancellationToken);
        if (!verifier.IsSuccess)
        {
            return Result<SecretSetup?>.Failure(verifier.Error!);
        }

        if (verifier.Value is not { } verifierText)
        {
            return Result<SecretSetup?>.Success(null);
        }

        var salt = await _settings.GetAsync(SaltKey, cancellationToken);
        var iterations = await _settings.GetAsync(IterationsKey, cancellationToken);
        if (!salt.IsSuccess || !iterations.IsSuccess)
        {
            return Result<SecretSetup?>.Failure(new Error(ErrorCategory.Storage, "The master password settings are incomplete."));
        }

        if (salt.Value is null || !int.TryParse(iterations.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var count))
        {
            return Result<SecretSetup?>.Failure(new Error(ErrorCategory.Storage, "The master password settings are incomplete."));
        }

        return Result<SecretSetup?>.Success(new SecretSetup(Convert.FromBase64String(salt.Value), count, verifierText));
    }
}
