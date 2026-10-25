using Avalonia.Platform.Storage;
using RestClient.Domain.Results;

namespace RestClient.UI.Services;

public sealed class StorageFilePickerService : IFilePickerService
{
    public async Task<Result<string?>> PickFileAsync()
    {
        if (MainWindowLocator.Find() is not { } owner)
        {
            return Result<string?>.Failure(new Error(ErrorCategory.Unexpected, "The file dialog has no window to attach to."));
        }

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a file",
            AllowMultiple = false,
        });
        if (files.Count == 0)
        {
            return Result<string?>.Success(null);
        }

        var path = files[0].TryGetLocalPath();
        if (path is null)
        {
            return Result<string?>.Failure(ResultErrors.Validation("The selected file has no local path."));
        }

        return Result<string?>.Success(path);
    }
}
