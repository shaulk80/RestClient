namespace RestDraft.Application.Secrets;

// What is stored to check a master password: the salt and iteration count for the key, and an encrypted verifier.
public sealed record SecretSetup(ReadOnlyMemory<byte> Salt, int Iterations, string Verifier);
