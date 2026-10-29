using RestDraft.Domain.Collections;
using RestDraft.UI.Services;

namespace RestDraft.UI.ViewModels;

public sealed class SaveDestinationViewModel : ViewModelBase
{
    private string _name;
    private FolderChoice? _selectedFolder;

    public SaveDestinationViewModel(string suggestedName, IReadOnlyList<FolderChoice> folders)
    {
        _name = suggestedName;
        Folders = folders;
        _selectedFolder = folders.Count > 0 ? folders[0] : null;
    }

    public IReadOnlyList<FolderChoice> Folders { get; }

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                RaisePropertyChanged(nameof(CanSave));
                RaisePropertyChanged(nameof(NameWarning));
            }
        }
    }

    public FolderChoice? SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            if (SetProperty(ref _selectedFolder, value))
            {
                RaisePropertyChanged(nameof(CanSave));
                RaisePropertyChanged(nameof(NameWarning));
            }
        }
    }

    public bool CanSave => SelectedFolder is not null && !string.IsNullOrWhiteSpace(Name);

    // The save still goes ahead when the name is taken, under the next free name. The warning says which.
    public string NameWarning
    {
        get
        {
            if (!CanSave || SelectedFolder is not { } choice)
            {
                return string.Empty;
            }

            var name = Name.Trim();
            if (ChildNameRules.ValidateChildName(name, choice.Folder, null).IsSuccess)
            {
                return string.Empty;
            }

            return $"'{name}' is already used in this folder. The request will be saved as '{ChildNameRules.NextAvailableName(name, choice.Folder)}'.";
        }
    }

    public SaveDestination? Destination
    {
        get
        {
            if (!CanSave || SelectedFolder is not { } folder)
            {
                return null;
            }

            return new SaveDestination(folder.FolderId, folder.ExpandId, Name.Trim());
        }
    }
}
