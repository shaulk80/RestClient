using System.Security.Cryptography;
using System.Text;
using RestDraft.Application.Abstractions;

namespace RestDraft.Application.Tests.Secrets;

// Deterministic and fast. The key depends on the password and the salt, which is what the tests need.
internal sealed class FakeKeyDeriver : IKeyDeriver
{
    public byte[] Derive(string password, ReadOnlyMemory<byte> salt, int iterations)
    {
        var input = Encoding.UTF8.GetBytes(password + "|" + Convert.ToBase64String(salt.Span));
        return SHA256.HashData(input);
    }
}
