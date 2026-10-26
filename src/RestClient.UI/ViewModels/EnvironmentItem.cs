using RestClient.Domain.Environments;

namespace RestClient.UI.ViewModels;

// An environment as the editor lists it. Variables hold stored values, so a secret is still encrypted here.
public sealed record EnvironmentItem(Guid Id, string Name, IReadOnlyList<Variable> Variables);
