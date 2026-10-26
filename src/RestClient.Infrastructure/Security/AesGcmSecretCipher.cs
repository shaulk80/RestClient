using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using RestClient.Application.Abstractions;
using RestClient.Domain.Results;

namespace RestClient.Infrastructure.Security;

// Stored form: base64 of nonce (12 bytes), tag (16 bytes), then the ciphertext.
public sealed class AesGcmSecretCipher : ISecretCipher
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private static readonly Action<ILogger, Exception?> EncryptFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(30, nameof(EncryptFailed)),
        "A secret could not be encrypted");

    private static readonly Action<ILogger, Exception?> DecryptFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(31, nameof(DecryptFailed)),
        "A secret could not be decrypted");

    private readonly ILogger<AesGcmSecretCipher> _logger;

    public AesGcmSecretCipher(ILogger<AesGcmSecretCipher> logger)
    {
        _logger = logger;
    }

    public Result<string> Encrypt(string plainText, ReadOnlyMemory<byte> key)
    {
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var plain = Encoding.UTF8.GetBytes(plainText);
            var cipher = new byte[plain.Length];
            var tag = new byte[TagSize];
            using var aes = new AesGcm(key.Span, TagSize);
            aes.Encrypt(nonce, plain, cipher, tag);
            return Result<string>.Success(Convert.ToBase64String([.. nonce, .. tag, .. cipher]));
        }
        catch (CryptographicException exception)
        {
            EncryptFailed(_logger, exception);
            return Result<string>.Failure(new Error(ErrorCategory.Security, "The secret could not be encrypted.", exception.ToString()));
        }
    }

    public Result<string> Decrypt(string cipherText, ReadOnlyMemory<byte> key)
    {
        try
        {
            return DecryptPayload(Convert.FromBase64String(cipherText), key);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            return DecryptionFailed(exception);
        }
    }

    private Result<string> DecryptPayload(byte[] payload, ReadOnlyMemory<byte> key)
    {
        if (payload.Length < NonceSize + TagSize)
        {
            return DecryptionFailed(null);
        }

        var nonce = payload[..NonceSize];
        var tag = payload[NonceSize..(NonceSize + TagSize)];
        var cipher = payload[(NonceSize + TagSize)..];
        var plain = new byte[cipher.Length];
        try
        {
            using var aes = new AesGcm(key.Span, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Result<string>.Success(Encoding.UTF8.GetString(plain));
        }
        catch (CryptographicException exception)
        {
            return DecryptionFailed(exception);
        }
    }

    // A wrong key and a changed ciphertext look the same from outside. Both are Security errors.
    private Result<string> DecryptionFailed(Exception? exception)
    {
        DecryptFailed(_logger, exception);
        return Result<string>.Failure(new Error(ErrorCategory.Security, "The secret could not be decrypted.", exception?.ToString()));
    }
}
