using System.Collections.ObjectModel;
using System.Windows.Input;
using RestClient.Application.Collections;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;
using RestClient.UI.Commands;
using RestClient.UI.Services;

namespace RestClient.UI.ViewModels;

public sealed class CollectionsTreeViewModel : ViewModelBase
{
    private const string NewCollectionName = "New collection";

    private readonly ICollectionService _collections;
    private readonly IFolderService _folders;
    private readonly IRequestService _requests;
    private readonly ICollectionTreeService _tree;
    private readonly IConfirmationDialogs _confirmations;
    private readonly AsyncRelayCommand _newCollectionCommand;
    private readonly RelayCommand _renameSelectedCommand;
    private readonly AsyncRelayCommand _deleteSelectedCommand;
    private readonly RelayCommand _openSelectedCommand;
    private HashSet<Guid> _expandedIds = [];
    private TreeNodeViewModel? _selectedNode;
    private string _statusMessage = string.Empty;

    public CollectionsTreeViewModel(
        ICollectionService collections,
        IFolderService folders,
        IRequestService requests,
        ICollectionTreeService tree,
        IConfirmationDialogs confirmations)
    {
        _collections = collections;
        _folders = folders;
        _requests = requests;
        _tree = tree;
        _confirmations = confirmations;
        _newCollectionCommand = new AsyncRelayCommand(CreateCollectionAsync, () => true);
        _renameSelectedCommand = new RelayCommand(RenameSelected, () => true);
        _deleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedAsync, () => true);
        _openSelectedCommand = new RelayCommand(OpenSelected, () => true);
    }

    public event Action<Guid>? RequestOpenRequested;

    public event EventHandler? TreeChanged;

    public ObservableCollection<CollectionNodeViewModel> Roots { get; } = [];

    public TreeNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set => SetProperty(ref _selectedNode, value);
    }

    // From the settings. A single click on a request opens it. Otherwise it takes a double-click.
    public bool OpensOnSingleClick { get; set; }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand NewCollectionCommand => _newCollectionCommand;

    // Keyboard shortcuts act on the selected node. They do nothing when nothing is selected.
    public ICommand RenameSelectedCommand => _renameSelectedCommand;

    public ICommand DeleteSelectedCommand => _deleteSelectedCommand;

    public ICommand OpenSelectedCommand => _openSelectedCommand;

    public bool WasExpanded(Guid id)
    {
        return _expandedIds.Contains(id);
    }

    public RequestNodeViewModel? FindRequest(Guid requestId)
    {
        return AllNodes().OfType<RequestNodeViewModel>().FirstOrDefault(node => node.Id == requestId);
    }

    public async Task ReloadAsync(Guid? selectId = null, Guid? expandId = null)
    {
        _expandedIds = AllNodes().Where(node => node.IsExpanded).Select(node => node.Id).ToHashSet();
        if (expandId is { } parentId)
        {
            _expandedIds.Add(parentId);
        }

        var loaded = await _collections.ListAsync(CancellationToken.None);
        if (!loaded.IsSuccess)
        {
            StatusMessage = ErrorMessageFormatter.Format(loaded.Error!);
            return;
        }

        Roots.Clear();
        foreach (var collection in loaded.Value!)
        {
            Roots.Add(new CollectionNodeViewModel(this, collection));
        }

        SelectedNode = selectId is { } id ? AllNodes().FirstOrDefault(node => node.Id == id) : null;
        StatusMessage = string.Empty;
        TreeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void OpenRequest(Guid requestId)
    {
        RequestOpenRequested?.Invoke(requestId);
    }

    public async Task CommitRenameAsync(TreeNodeViewModel node)
    {
        var name = node.EditName.Trim();
        if (name.Length == 0 || name == node.Name)
        {
            node.EndRename();
            return;
        }

        var result = await RenameNodeAsync(node, name);
        node.EndRename();
        if (!result.IsSuccess)
        {
            StatusMessage = ErrorMessageFormatter.Format(result.Error!);
            return;
        }

        await ReloadAsync(node.Id);
    }

    // Nothing is deleted unless the user confirms. Open tabs of a deleted request are closed by the window.
    public async Task DeleteAsync(TreeNodeViewModel node)
    {
        var confirmed = await _confirmations.ConfirmAsync(
            $"Delete '{node.Name}'? Its contents and any unsaved edits in open tabs are lost. This cannot be undone.",
            CancellationToken.None);
        if (!confirmed)
        {
            return;
        }

        var result = await DeleteNodeAsync(node);
        if (!result.IsSuccess)
        {
            StatusMessage = ErrorMessageFormatter.Format(result.Error!);
            return;
        }

        await ReloadAsync();
    }

    public async Task CreateFolderAsync(TreeNodeViewModel parent)
    {
        var result = await _folders.CreateAsync(parent.DropFolderId, CancellationToken.None);
        if (!result.IsSuccess)
        {
            StatusMessage = ErrorMessageFormatter.Format(result.Error!);
            return;
        }

        await ReloadAsync(result.Value, parent.Id);
        SelectedNode?.BeginRename();
    }

    public async Task CreateRequestAsync(TreeNodeViewModel parent)
    {
        var result = await _requests.CreateAsync(parent.DropFolderId, CancellationToken.None);
        if (!result.IsSuccess)
        {
            StatusMessage = ErrorMessageFormatter.Format(result.Error!);
            return;
        }

        var requestId = result.Value;
        await ReloadAsync(requestId, parent.Id);
        RequestOpenRequested?.Invoke(requestId);
    }

    public async Task DuplicateAsync(TreeNodeViewModel request)
    {
        var result = await _requests.DuplicateAsync(request.Id, CancellationToken.None);
        if (!result.IsSuccess)
        {
            StatusMessage = ErrorMessageFormatter.Format(result.Error!);
            return;
        }

        await ReloadAsync(result.Value);
    }

    public async Task MoveAsync(TreeNodeViewModel source, TreeNodeViewModel target)
    {
        if (source.Id == target.Id)
        {
            return;
        }

        if (source is CollectionNodeViewModel)
        {
            StatusMessage = "Collections cannot be moved.";
            return;
        }

        var result = await _tree.MoveAsync(source.Id, target.DropFolderId, target.DropBeforeId, CancellationToken.None);
        if (!result.IsSuccess)
        {
            StatusMessage = ErrorMessageFormatter.Format(result.Error!);
            return;
        }

        var expandId = target.IsRequest ? target.DropFolderId : target.Id;
        await ReloadAsync(source.Id, expandId);
    }

    // The keys also reach the tree while a rename box has focus, where Delete and Enter belong to the box.
    private void RenameSelected()
    {
        if (SelectedNode is { IsEditing: false } node)
        {
            node.BeginRename();
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (SelectedNode is { IsEditing: false } node)
        {
            await DeleteAsync(node);
        }
    }

    private void OpenSelected()
    {
        if (SelectedNode is RequestNodeViewModel { IsEditing: false } request)
        {
            OpenRequest(request.Id);
        }
    }

    private async Task CreateCollectionAsync()
    {
        var result = await _collections.CreateAsync(NewCollectionName, CancellationToken.None);
        if (!result.IsSuccess)
        {
            StatusMessage = ErrorMessageFormatter.Format(result.Error!);
            return;
        }

        await ReloadAsync(result.Value);
        SelectedNode?.BeginRename();
    }

    private Task<Result> RenameNodeAsync(TreeNodeViewModel node, string name)
    {
        return node switch
        {
            CollectionNodeViewModel => _collections.RenameAsync(node.Id, name, CancellationToken.None),
            FolderNodeViewModel => _folders.RenameAsync(node.Id, name, CancellationToken.None),
            RequestNodeViewModel => _requests.RenameAsync(node.Id, name, CancellationToken.None),
            _ => Task.FromResult(Result.Failure(ResultErrors.Validation("This item cannot be renamed."))),
        };
    }

    private Task<Result> DeleteNodeAsync(TreeNodeViewModel node)
    {
        return node switch
        {
            CollectionNodeViewModel => _collections.DeleteAsync(node.Id, CancellationToken.None),
            FolderNodeViewModel => _folders.DeleteAsync(node.Id, CancellationToken.None),
            RequestNodeViewModel => _requests.DeleteAsync(node.Id, CancellationToken.None),
            _ => Task.FromResult(Result.Failure(ResultErrors.Validation("This item cannot be deleted."))),
        };
    }

    private IEnumerable<TreeNodeViewModel> AllNodes()
    {
        return Roots.SelectMany(root => Flatten(root));
    }

    private static IEnumerable<TreeNodeViewModel> Flatten(TreeNodeViewModel node)
    {
        return [node, .. node.Children.SelectMany(child => Flatten(child))];
    }
}
