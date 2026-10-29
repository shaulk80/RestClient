using System.Collections.ObjectModel;
using System.Windows.Input;
using RestDraft.Application.Environments;
using RestDraft.Domain.Environments;
using RestDraft.UI.Commands;
using RestDraft.UI.Services;

namespace RestDraft.UI.ViewModels;

public sealed class VariablesEditorViewModel : ViewModelBase
{
    private readonly Guid _environmentId;
    private readonly IEnvironmentService _environments;
    private readonly ISecretPrompt _secrets;
    private readonly AsyncRelayCommand _saveCommand;
    private string _message = string.Empty;

    public VariablesEditorViewModel(
        Guid environmentId,
        IReadOnlyList<Variable> variables,
        IEnvironmentService environments,
        ISecretPrompt secrets)
    {
        _environmentId = environmentId;
        _environments = environments;
        _secrets = secrets;
        Rows = new ObservableCollection<VariableRowViewModel>(variables.Select(variable => new VariableRowViewModel(variable, Remove)));
        _saveCommand = new AsyncRelayCommand(() => SaveAsync(), () => true);
        AddCommand = new RelayCommand(Add, () => true);
    }

    // Raised after a save that stored the rows. The editor reloads the environment on it.
    public event EventHandler? Saved;

    public ObservableCollection<VariableRowViewModel> Rows { get; }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public ICommand AddCommand { get; }

    public ICommand SaveCommand => _saveCommand;

    public async Task<bool> SaveAsync()
    {
        var edits = Rows.Select(row => row.ToEdit()).ToList();
        if (edits.Any(edit => edit.IsSecret && edit.ValueIsPlainText))
        {
            var unlocked = await _secrets.EnsureUnlockedAsync(CancellationToken.None);
            if (!unlocked.IsSuccess || !unlocked.Value)
            {
                Message = unlocked.IsSuccess ? "Unlock the secrets to save secret values." : ErrorMessageFormatter.Format(unlocked.Error!);
                return false;
            }
        }

        var result = await _environments.UpdateVariablesAsync(_environmentId, edits, CancellationToken.None);
        if (!result.IsSuccess)
        {
            Message = ErrorMessageFormatter.Format(result.Error!);
            return false;
        }

        Message = "Saved.";
        Saved?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void Add()
    {
        Rows.Add(new VariableRowViewModel(null, Remove));
    }

    private void Remove(VariableRowViewModel row)
    {
        Rows.Remove(row);
    }
}
