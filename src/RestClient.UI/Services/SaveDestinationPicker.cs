using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using RestClient.Application.Collections;
using RestClient.Domain.Results;
using RestClient.UI.ViewModels;
using RestClient.UI.Views;

namespace RestClient.UI.Services;

public sealed class SaveDestinationPicker : IDestinationPicker
{
    private readonly ICollectionService _collections;

    public SaveDestinationPicker(ICollectionService collections)
    {
        _collections = collections;
    }

    public async Task<Result<SaveDestination?>> PickAsync(string suggestedName, CancellationToken cancellationToken)
    {
        var listed = await _collections.ListAsync(cancellationToken);
        if (!listed.IsSuccess)
        {
            return Result<SaveDestination?>.Failure(listed.Error!);
        }

        var folders = FolderChoices.From(listed.Value!);
        if (folders.Count == 0)
        {
            return Result<SaveDestination?>.Failure(ResultErrors.Validation("Create a collection first, then save into it."));
        }

        if (MainOwner() is not { } owner)
        {
            return Result<SaveDestination?>.Failure(new Error(ErrorCategory.Unexpected, "The save dialog has no window to attach to."));
        }

        var window = new SaveDestinationWindow
        {
            DataContext = new SaveDestinationViewModel(suggestedName, folders),
        };
        var destination = await window.ShowDialog<SaveDestination?>(owner);
        return Result<SaveDestination?>.Success(destination);
    }

    private static Window? MainOwner()
    {
        return (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    }
}
