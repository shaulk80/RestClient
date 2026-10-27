using System.Collections.ObjectModel;
using System.Windows.Input;
using RestClient.Application.Environments;
using RestClient.UI.Commands;
using RestClient.UI.Services;

namespace RestClient.UI.ViewModels;

public sealed class EnvironmentSelectorViewModel : ViewModelBase
{
    private static readonly EnvironmentChoice NoEnvironment = new(null, "No environment");

    private readonly IEnvironmentService _environments;
    private readonly IEnvironmentDialogs _dialogs;
    private readonly AsyncRelayCommand _manageCommand;
    private EnvironmentChoice? _selectedChoice;
    private string _message = string.Empty;

    public EnvironmentSelectorViewModel(IEnvironmentService environments, IEnvironmentDialogs dialogs)
    {
        _environments = environments;
        _dialogs = dialogs;
        _manageCommand = new AsyncRelayCommand(() => ManageAsync(), () => true);
    }

    public ObservableCollection<EnvironmentChoice> Choices { get; } = [];

    public EnvironmentChoice? SelectedChoice
    {
        get => _selectedChoice;
        private set => SetProperty(ref _selectedChoice, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public ICommand ManageCommand => _manageCommand;

    public async Task ReloadAsync()
    {
        var listed = await _environments.ListAsync(CancellationToken.None);
        var active = await _environments.GetActiveAsync(CancellationToken.None);
        if (!listed.IsSuccess || !active.IsSuccess)
        {
            Message = ErrorMessageFormatter.Format((listed.Error ?? active.Error)!);
            return;
        }

        // EnvironmentChoice has value equality, so reset first to make the combo re-select after the list is rebuilt.
        SelectedChoice = null;
        Choices.Clear();
        Choices.Add(NoEnvironment);
        foreach (var environment in listed.Value!)
        {
            Choices.Add(new EnvironmentChoice(environment.Id, environment.Name));
        }

        SelectedChoice = Choices.FirstOrDefault(choice => choice.Id == active.Value?.Id) ?? NoEnvironment;
        Message = string.Empty;
    }

    // Called when the user picks a choice in the status bar. The combo box also reports the selection that
    // ReloadAsync made, which is already stored, so that one is skipped.
    public async Task SelectAsync(EnvironmentChoice? choice)
    {
        if (choice == SelectedChoice)
        {
            return;
        }

        SelectedChoice = choice;
        var saved = await _environments.SetActiveAsync(choice?.Id, CancellationToken.None);
        Message = saved.IsSuccess ? string.Empty : ErrorMessageFormatter.Format(saved.Error!);
    }

    private async Task ManageAsync()
    {
        await _dialogs.ShowEditorAsync(CancellationToken.None);
        await ReloadAsync();
    }
}
