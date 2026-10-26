namespace RestClient.Infrastructure.Persistence.Dto;

// For a secret, Value holds the ciphertext. The storage writer decides which column it goes to.
public sealed record VariableDto(string Name, string Value, bool IsSecret);
