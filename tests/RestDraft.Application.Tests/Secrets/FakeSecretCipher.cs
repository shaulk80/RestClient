using System.Text;
using RestDraft.Application.Abstractions;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Tests.Secrets;

// Not encryption. It tags each value with its key, so a wrong key fails the way AES-GCM does.
internal sealed class FakeSecretCipher : ISecretCipher
{
    private const string Separator = "|";

    public Result<string> Encrypt(string plainText, ReadOnlyMemory<byte> key)
    {
        return Result<string>.Success(KeyTag(key) + Separator + plainText);
    }

    public Result<string> Decrypt(string cipherText, ReadOnlyMemory<byte> key)
    {
        var prefix = KeyTag(key) + Separator;
        return cipherText.StartsWith(prefix, StringComparison.Ordinal)
            ? Result<string>.Success(cipherText[prefix.Length..])
            : Result<string>.Failure(new Error(ErrorCategory.Security, "The secret could not be decrypted."));
    }

    private static string KeyTag(ReadOnlyMemory<byte> key)
    {
        return Convert.ToBase64String(key.Span);
    }
}
