using System.Collections.ObjectModel;
using System.Windows.Input;
using RestClient.Application.Environments;
using RestClient.Domain.Environments;
using RestClient.UI.Commands;
using RestClient.UI.Services;

namespace RestClient.UI.ViewModels;

public sealed class EnvironmentEditorViewModel : ViewModelBase
{
    private readonly IEnvironmentService _environments;
    private readonly ISecretPrompt _secrets;
    private readonly IConfirmationDialogs _confirmations;
    private readonly AsyncRelayCommand _createCommand;
    private readonly AsyncRelayCommand _renameCommand;
    private readonly AsyncRelayCommand _deleteCommand;
    private EnvironmentItem? _selected;
    private VariablesEditorViewModel? _variables;
    private string _newName = string.Empty;
    private string _renameTo = string.Empty;
    private string _message = string.Empty;

    public EnvironmentEditorViewModel(IEnvironmentService environments, ISecretPrompt secrets, IConfirmationDialogs confirmations)
    {
        _environments = environments;
        _secrets = secrets;
        _confirmations = confirmations;
        _createCommand = new AsyncRelayCommand(() => CreateAsync(), () => NewName.Trim().Length > 0);
        _renameCommand = new AsyncRelayCommand(() => RenameAsync(), () => _selected is not null);
        _deleteCommand = new AsyncRelayCommand(() => DeleteAsync(), () => _selected is not null);
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
                RenameTo = value?.Name ?? string.Empty;
                _renameCommand.RaiseCanExecuteChanged();
                _deleteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public VariablesEditorViewModel? Variables
    {
        get => _variables;
        private set => SetProperty(ref _variables, value);
    }

    public string NewName
    {
        get => _newName;
        set
        {
            if (SetProperty(ref _newName, value))
            {
                _createCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string RenameTo
    {
        get => _renameTo;
        set => SetProperty(ref _renameTo, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public ICommand CreateCommand => _createCommand;

    public ICommand RenameCommand => _renameCommand;

    public ICommand DeleteCommand => _deleteCommand;

    public async Task ReloadAsync()
    {
        var listed = await _environments.ListAsync(CancellationToken.None);
        if (!listed.IsSuccess)
        {
            Message = ErrorMessageFormatter.Format(listed.Error!);
            return;
        }

        var selectedId = _selected?.Id;
        Environments.Clear();
        foreach (var environment in listed.Value!)
        {
            Environments.Add(new EnvironmentItem(environment.Id, environment.Name, environment.Variables));
        }

        SelectedEnvironment = Environments.FirstOrDefault(item => item.Id == selectedId) ?? Environments.FirstOrDefault();
    }

    public async Task<bool> CreateAsync()
    {
        var created = await _environments.CreateAsync(NewName, CancellationToken.None);
        if (!created.IsSuccess)
        {
            Message = ErrorMessageFormatter.Format(created.Error!);
            return false;
        }

        NewName = string.Empty;
        await ReloadAsync();
        SelectedEnvironment = Environments.FirstOrDefault(item => item.Id == created.Value);
        return true;
    }

    public async Task<bool> RenameAsync()
    {
        if (_selected is not { } selected)
        {
            return false;
        }

        var renamed = await _environments.RenameAsync(selected.Id, RenameTo, CancellationToken.None);
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
