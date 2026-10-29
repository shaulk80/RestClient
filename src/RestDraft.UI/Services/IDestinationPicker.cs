using RestDraft.Domain.Results;

namespace RestDraft.UI.Services;

public interface IDestinationPicker
{
    // Success with a null value means the user cancelled.
    Task<Result<SaveDestination?>> PickAsync(string suggestedName, CancellationToken cancellationToken);
}
