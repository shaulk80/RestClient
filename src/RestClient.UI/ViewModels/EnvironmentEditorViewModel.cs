using System.Collections.ObjectModel;
using System.Windows.Input;
using RestClient.Application.Environments;
using RestClient.Domain.Environments;
using RestClient.UI.Commands;
using RestClient.UI.Services;

namespace RestClient.UI.ViewModels;

// The environments window. Names are edited in place in the list: Create adds an environment with a default
// name and starts renaming it. One environment is the current one, which requests use. It is marked in the list.
public sealed class EnvironmentEditorViewModel : ViewModelBase
{
    private readonly IEnvironmentService _environments;
    private readonly ISecretPrompt _secrets;
    private readonly IConfirmationDialogs _confirmations;
    private readonly AsyncRelayCommand _createCommand;
    private readonly RelayCommand _renameCommand;
    private readonly AsyncRelayCommand _deleteCommand;
    private readonly AsyncRelayCommand _setCurrentCommand;
    private EnvironmentItem? _selected;
    private VariablesEditorViewModel? _variables;
    private string _message = string.Empty;

    public EnvironmentEditorViewModel(IEnvironmentService environments, ISecretPrompt secrets, IConfirmationDialogs confirmations)
    {
        _environments = environments;
        _secrets = secrets;
        _confirmations = confirmations;
        _createCommand = new AsyncRelayCommand(() => CreateAsync(), () => true);
        _renameCommand = new RelayCommand(BeginRename, () => _selected is { IsEditing: false });
        _deleteCommand = new AsyncRelayCommand(() => DeleteAsync(), () => _selected is not null);
        _setCurrentCommand = new AsyncRelayCommand(() => SetCurrentAsync(), () => _selected is { IsCurrent: false });
    }

    public ObservableCollection<EnvironmentItem> Environments { get; } = [];

    public EnvironmentItem? SelectedEnvironment
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                Variables = value is null ? null : CreateVariables(value);
                RaiseSelectionCommands();
            }
        }
    }

    public VariablesEditorViewModel? Variables
    {
        get => _variables;
        private set => SetProperty(ref _variables, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public ICommand CreateCommand => _createCommand;

    public ICommand RenameCommand => _renameCommand;

    public ICommand DeleteCommand => _deleteCommand;

    public ICommand SetCurrentCommand => _setCurrentCommand;

    public async Task ReloadAsync()
    {
        var listed = await _environments.ListAsync(CancellationToken.None);
        var current = await _environments.GetActiveAsync(CancellationToken.None);
        if (!listed.IsSuccess || !current.IsSuccess)
        {
            Message = ErrorMessageFormatter.Format((listed.Error ?? current.Error)!);
            return;
        }

        Message = string.Empty;
        var selectedId = _selected?.Id;
        Environments.Clear();
        foreach (var environment in listed.Value!)
        {
            Environments.Add(new EnvironmentItem(this, environment.Id, environment.Name, environment.Variables, environment.Id == current.Value?.Id));
        }

        SelectedEnvironment = Environments.FirstOrDefault(item => item.Id == selectedId) ?? Environments.FirstOrDefault();
    }

    public async Task<bool> CreateAsync()
    {
        var name = NewEnvironmentName.NextAvailable(Environments.Select(item => item.Name));
        var created = await _environments.CreateAsync(name, CancellationToken.None);
        if (!created.IsSuccess)
        {
            Message = ErrorMessageFormatter.Format(created.Error!);
            return false;
        }

        await ReloadAsync();
        SelectedEnvironment = Environments.FirstOrDefault(item => item.Id == created.Value);
        SelectedEnvironment?.BeginRename();
        RaiseSelectionCommands();
        return true;
    }

    // Enter and losing the focus both commit. The second call finds the rename already over and does nothing,
    // which also keeps Escape (cancel, then focus lost) from committing.
    public async Task<bool> CommitRenameAsync(EnvironmentItem item)
    {
        if (!item.IsEditing)
        {
            return false;
        }

        var name = item.EditName.Trim();
        item.EndRename();
        RaiseSelectionCommands();
        if (name.Length == 0 || name == item.Name)
        {
            return false;
        }

        var renamed = await _environments.RenameAsync(item.Id, name, CancellationToken.None);
        if (!renamed.IsSuccess)
        {
            Message = ErrorMessageFormatter.Format(renamed.Error!);
            return false;
        }

        await ReloadAsync();
        return true;
    }

    // Deleting an environment also deletes its variables, including stored secrets, so the user confirms first.
    public async Task<bool> DeleteAsync()
    {
        if (_selected is not { } selected)
        {
            return false;
        }

        var confirmed = await _confirmations.ConfirmAsync(
            $"Delete the environment '{selected.Name}' and its variables, including stored secrets? This cannot be undone.",
            CancellationToken.None);
        if (!confirmed)
        {
            return false;
        }

        var deleted = await _environments.DeleteAsync(selected.Id, CancellationToken.None);
        if (!deleted.IsSuccess)
        {
            Message = ErrorMessageFormatter.Format(deleted.Error!);
            return false;
        }

        await ReloadAsync();
        return true;
    }

    public async Task<bool> SetCurrentAsync()
    {
        if (_selected is not { } selected)
        {
            return false;
        }

        var saved = await _environments.SetActiveAsync(selected.Id, CancellationToken.None);
        if (!saved.IsSuccess)
        {
            Message = ErrorMessageFormatter.Format(saved.Error!);
            return false;
        }

        Message = string.Empty;
        foreach (var item in Environments)
        {
            item.IsCurrent = item.Id == selected.Id;
        }

        RaiseSelectionCommands();
        return true;
    }

    private void BeginRename()
    {
        _selected?.BeginRename();
        RaiseSelectionCommands();
    }

    private void RaiseSelectionCommands()
    {
        _renameCommand.RaiseCanExecuteChanged();
        _deleteCommand.RaiseCanExecuteChanged();
        _setCurrentCommand.RaiseCanExecuteChanged();
    }

    private VariablesEditorViewModel CreateVariables(EnvironmentItem item)
    {
        var editor = new VariablesEditorViewModel(item.Id, item.Variables, _environments, _secrets);
        editor.Saved += (_, _) => ReloadOnSave();
        return editor;
    }

    private async void ReloadOnSave()
    {
        await ReloadAsync();
    }
}
