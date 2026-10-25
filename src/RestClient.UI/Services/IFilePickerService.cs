using RestClient.Domain.Results;

namespace RestClient.UI.Services;

public interface IFilePickerService
{
    // Success with a null value means the user cancelled.
    Task<Result<string?>> PickFileAsync();
}
