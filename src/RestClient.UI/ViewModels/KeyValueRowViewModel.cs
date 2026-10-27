using System.Windows.Input;
using RestClient.Domain.Collections;
using RestClient.UI.Commands;

namespace RestClient.UI.ViewModels;

public sealed class KeyValueRowViewModel : ViewModelBase
{
    private string _key;
    private string _value;
    private bool _isEnabled;

    // The label names the row for screen readers: "Header" gives "Header name", "Header value" and so on.
    public KeyValueRowViewModel(KeyValueEntry entry, string itemLabel = "Field")
    {
        NameAutomationName = $"{itemLabel} name";
        ValueAutomationName = $"{itemLabel} value";
        EnabledAutomationName = $"{itemLabel} enabled";
        RemoveAutomationName = $"Remove {itemLabel.ToLowerInvariant()}";
        _key = entry.Key;
        _value = entry.Value;
        _isEnabled = entry.IsEnabled;
        RemoveCommand = new RelayCommand(() => RemoveRequested?.Invoke(this, EventArgs.Empty), () => true);
    }

    public event EventHandler? Changed;

    public event EventHandler? RemoveRequested;

    public string Key
    {
        get => _key;
        set => EditField(ref _key, value, nameof(Key));
    }

    public string Value
    {
        get => _value;
        set => EditField(ref _value, value, nameof(Value));
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => EditField(ref _isEnabled, value, nameof(IsEnabled));
    }

    public ICommand RemoveCommand { get; }

    public string NameAutomationName { get; }

    public string ValueAutomationName { get; }

    public string EnabledAutomationName { get; }

    public string RemoveAutomationName { get; }

    public KeyValueEntry ToEntry()
    {
        return new KeyValueEntry(_key, _value, _isEnabled);
    }

    private void EditField<T>(ref T field, T value, string propertyName)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
