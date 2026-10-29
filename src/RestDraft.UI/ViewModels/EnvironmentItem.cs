using System.Windows.Input;
using RestDraft.Domain.Environments;
using RestDraft.UI.Commands;

namespace RestDraft.UI.ViewModels;

// An environment as the editor lists it. Variables hold stored values, so a secret is still encrypted here.
// The name is edited in place in the list, the same as a node of the Collections tree.
public sealed class EnvironmentItem : ViewModelBase
{
    private bool _isCurrent;
    private bool _isEditing;
    private string _editName = string.Empty;

    public EnvironmentItem(EnvironmentEditorViewModel owner, Guid id, string name, IReadOnlyList<Variable> variables, bool isCurrent)
    {
        Id = id;
        Name = name;
        Variables = variables;
        _isCurrent = isCurrent;
        CommitRenameCommand = new AsyncRelayCommand(() => owner.CommitRenameAsync(this), () => true);
        CancelRenameCommand = new RelayCommand(EndRename, () => true);
    }

    public Guid Id { get; }

    public string Name { get; }

    public IReadOnlyList<Variable> Variables { get; }

    // The environment that requests use now. It is marked in the list.
    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            if (SetProperty(ref _isCurrent, value))
            {
                RaisePropertyChanged(nameof(AutomationName));
            }
        }
    }

    public string AutomationName => IsCurrent ? $"{Name}, current environment" : Name;

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

    public ICommand CommitRenameCommand { get; }

    public ICommand CancelRenameCommand { get; }

    public void BeginRename()
    {
        EditName = Name;
        IsEditing = true;
    }

    public void EndRename()
    {
        IsEditing = false;
    }
}
