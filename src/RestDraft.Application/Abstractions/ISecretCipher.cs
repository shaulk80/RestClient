using RestDraft.Domain.Results;

namespace RestDraft.Application.Abstractions;

public interface ISecretCipher
{
    // Returns the encrypted form as text. Never throws for expected failures.
    Result<string> Encrypt(string plainText, ReadOnlyMemory<byte> key);

    // A wrong key or a changed ciphertext gives a Security error.
    Result<string> Decrypt(string cipherText, ReadOnlyMemory<byte> key);
}
