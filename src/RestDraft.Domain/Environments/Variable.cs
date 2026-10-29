namespace RestDraft.Domain.Environments;

// For a secret, Value holds the encrypted form, never the plain text. Plain text exists only in the editor
// while the user types it, and in memory during a send.
public sealed record Variable(string Name, string Value, bool IsSecret);
