using System.Windows.Input;
using RestClient.Application.Environments;
using RestClient.Domain.Environments;
using RestClient.UI.Commands;

namespace RestClient.UI.ViewModels;

// One variable row. A stored secret shows no value. Its ciphertext is kept until the user types a new one.
public sealed class VariableRowViewModel : ViewModelBase
{
    private readonly Action<VariableRowViewModel> _remove;
    private string? _storedCiphertext;
    private string _name;
    private string _value;
    private bool _isSecret;
    private bool _valueTyped;

    public VariableRowViewModel(Variable? variable, Action<VariableRowViewModel> remove)
    {
        _remove = remove;
        _name = variable?.Name ?? string.Empty;
        _isSecret = variable?.IsSecret ?? false;
        if (variable is { IsSecret: true })
        {
            _storedCiphertext = variable.Value;
            _value = string.Empty;
            _valueTyped = false;
        }
        else
        {
            _value = variable?.Value ?? string.Empty;
            _valueTyped = true;
        }

        RemoveCommand = new RelayCommand(() => _remove(this), () => true);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
            {
                _valueTyped = true;
                RaisePropertyChanged(nameof(ValueHint));
            }
        }
    }

    public bool IsSecret
    {
        get => _isSecret;
        set
        {
            if (SetProperty(ref _isSecret, value))
            {
                ClearStoredCiphertextWhenPlain();
                RaisePropertyChanged(nameof(ValueHint));
                RaisePropertyChanged(nameof(PasswordChar));
            }
        }
    }

    // Masks the value box while the row is a secret.
    public char PasswordChar => IsSecret ? '*' : '\0';

    // Shown in the value box. A stored secret says it is kept until replaced.
    public string ValueHint
    {
        get
        {
            if (!IsSecret)
            {
                return "Value";
            }

            return _valueTyped ? "Secret value" : "Stored. Type a value to replace it.";
        }
    }

    public ICommand RemoveCommand { get; }

    // A stored secret that the user did not retype keeps its ciphertext. Any other row sends its typed value.
    public VariableEdit ToEdit()
    {
        return IsSecret && !_valueTyped
            ? new VariableEdit(Name, _storedCiphertext ?? string.Empty, true, false)
            : new VariableEdit(Name, Value, IsSecret, true);
    }

    // A stored secret that becomes plain has no plain value to show, so the user types one.
    private void ClearStoredCiphertextWhenPlain()
    {
        if (IsSecret || _storedCiphertext is null)
        {
            return;
        }

        _storedCiphertext = null;
        Value = string.Empty;
        _valueTyped = true;
    }
}
