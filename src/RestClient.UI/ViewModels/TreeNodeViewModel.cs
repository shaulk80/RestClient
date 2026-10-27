using System.Windows.Input;
using RestClient.Domain.Collections;
using RestClient.UI.Commands;

namespace RestClient.UI.ViewModels;

public abstract class TreeNodeViewModel : ViewModelBase
{
    private readonly CollectionsTreeViewModel _owner;
    private bool _isEditing;
    private string _editName = string.Empty;
    private bool _isExpanded;

    protected TreeNodeViewModel(CollectionsTreeViewModel owner, Guid id, string name)
    {
        _owner = owner;
        Id = id;
        Name = name;
        _isExpanded = owner.WasExpanded(id);
        RenameCommand = new RelayCommand(BeginRename, () => true);
        CommitRenameCommand = new AsyncRelayCommand(() => _owner.CommitRenameAsync(this), () => true);
        CancelRenameCommand = new RelayCommand(EndRename, () => true);
        DeleteCommand = new AsyncRelayCommand(() => _owner.DeleteAsync(this), () => true);
        NewFolderCommand = new AsyncRelayCommand(() => _owner.CreateFolderAsync(this), () => true);
        NewRequestCommand = new AsyncRelayCommand(() => _owner.CreateRequestAsync(this), () => true);
        DuplicateCommand = new AsyncRelayCommand(() => _owner.DuplicateAsync(this), () => true);
        OpenCommand = new RelayCommand(() => _owner.OpenRequest(Id), () => true);
        NewCollectionCommand = _owner.NewCollectionCommand;
    }

    public Guid Id { get; }

    public string Name { get; }

    public bool IsEditing
    {
        get => _isEditing;
        private set => SetProperty(ref _isEditing, value);
    }

    public string EditName
    {
        get => _editName;
        set => SetProperty(ref _editName, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public IReadOnlyList<TreeNodeViewModel> Children { get; protected set; } = [];

    public abstract bool CanContainChildren { get; }

    public virtual bool IsRequest => false;

    public virtual bool IsCollection => false;

    // Folder that receives a node dropped on this one, and the sibling to insert before (null appends).
    public abstract Guid DropFolderId { get; }

    public abstract Guid? DropBeforeId { get; }

    public ICommand RenameCommand { get; }

    public ICommand CommitRenameCommand { get; }

    public ICommand CancelRenameCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand NewFolderCommand { get; }

    public ICommand NewRequestCommand { get; }

    public ICommand DuplicateCommand { get; }

    public ICommand OpenCommand { get; }

    public ICommand NewCollectionCommand { get; }

    public void BeginRename()
    {
        EditName = Name;
        IsEditing = true;
    }

    public void EndRename()
    {
        IsEditing = false;
    }

    protected static IReadOnlyList<TreeNodeViewModel> BuildChildren(CollectionsTreeViewModel owner, Folder folder)
    {
        return [
            .. folder.Folders.Select(child => new FolderNodeViewModel(owner, child)),
            .. folder.Requests.Select(request => new RequestNodeViewModel(owner, request, folder.Id)),
        ];
    }
}
