namespace RestDraft.Application.Environments;

// One row from the editor. For a secret, ValueIsPlainText is true only when the user typed a new value.
// A false value means the stored ciphertext is kept as it is.
public sealed record VariableEdit(string Name, string Value, bool IsSecret, bool ValueIsPlainText);
