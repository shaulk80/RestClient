namespace RestDraft.UI.ViewModels;

// A row in the status bar selector. A null Id is "No environment".
public sealed record EnvironmentChoice(Guid? Id, string Name);
