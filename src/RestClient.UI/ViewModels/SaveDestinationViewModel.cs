using RestClient.UI.Services;

namespace RestClient.UI.ViewModels;

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
            }
        }
    }

    public bool CanSave => SelectedFolder is not null && !string.IsNullOrWhiteSpace(Name);

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
