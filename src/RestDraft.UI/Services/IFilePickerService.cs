using RestDraft.Domain.Results;

namespace RestDraft.UI.Services;

public interface IFilePickerService
{
    // Success with a null value means the user cancelled.
    Task<Result<string?>> PickFileAsync();
}
