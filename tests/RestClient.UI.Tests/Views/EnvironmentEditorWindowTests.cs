using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FakeItEasy;
using NUnit.Framework;
using RestClient.Application.Environments;
using RestClient.Domain.Environments;
using RestClient.Domain.Results;
using RestClient.UI.Services;
using RestClient.UI.ViewModels;
using RestClient.UI.Views;

namespace RestClient.UI.Tests.Views;

[TestFixture]
public class EnvironmentEditorWindowTests
{
    private static readonly Guid StagingId = Guid.NewGuid();
    private static readonly Guid ProductionId = Guid.NewGuid();
    private static readonly string[] ToolButtonNames = ["Create environment", "Rename environment", "Delete environment"];
    private static readonly string[] ContextMenuHeaders = ["Set as _current", "C_reate", "_Rename", "_Delete"];
    private static readonly string[] MarkedEnvironments = ["Production"];

    private IEnvironmentService _environments = null!;
    private IConfirmationDialogs _confirmations = null!;
    private EnvironmentEditorViewModel _viewModel = null!;
    private EnvironmentEditorWindow _window = null!;

    [TearDown]
    public void TearDown()
    {
        _window.Close();
    }

    [AvaloniaTest]
    public async Task ControlsAreNamedWithEnvironmentsInTheList()
    {
        await OpenWindowAsync();

        Assert.That(AccessibleNameChecker.FindUnnamed(_window), Is.Empty);
    }

    [AvaloniaTest]
    public async Task DialogOpensWithTheSelectedEnvironmentFocused()
    {
        await OpenWindowAsync();
        await Task.Delay(50);
        Dispatcher.UIThread.RunJobs();

        var focused = _window.FocusManager?.GetFocusedElement();

        Assert.That(focused, Is.TypeOf<ListBoxItem>());
        Assert.That(((ListBoxItem)focused!).DataContext, Is.SameAs(_viewModel.SelectedEnvironment));
    }

    [AvaloniaTest]
    public async Task ThreeSmallToolButtonsHaveTooltipsAndNames()
    {
        await OpenWindowAsync();

        var tools = _window.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("tool")).ToList();

        Assert.That(tools, Has.Count.EqualTo(3));
        Assert.That(tools.Select(button => Avalonia.Automation.AutomationProperties.GetName(button)),
            Is.EqualTo(ToolButtonNames));
        Assert.That(tools.Select(button => ToolTip.GetTip(button)), Is.All.Not.Null);
        Assert.That(tools.Select(button => button.Content), Is.All.TypeOf<Avalonia.Controls.Shapes.Path>());
    }

    [AvaloniaTest]
    public async Task ThereIsNoSeparateNameBox()
    {
        await OpenWindowAsync();

        var names = _window.GetVisualDescendants().OfType<TextBox>()
            .Select(box => Avalonia.Automation.AutomationProperties.GetName(box));

        Assert.That(names, Does.Not.Contain("New environment name"));
        Assert.That(names, Does.Not.Contain("Rename selected environment to"));
    }

    [AvaloniaTest]
    public async Task RenamingShowsTheNameBoxInTheListWithTheTextSelected()
    {
        await OpenWindowAsync();

        _viewModel.SelectedEnvironment = _viewModel.Environments[0];
        _viewModel.RenameCommand.Execute(null);
        await Task.Delay(50);
        Dispatcher.UIThread.RunJobs();

        var focused = _window.FocusManager?.GetFocusedElement() as TextBox;
        Assert.That(focused, Is.Not.Null);
        Assert.That(focused!.Text, Is.EqualTo("Staging"));
        Assert.That(focused.SelectedText, Is.EqualTo("Staging"));
    }

    [AvaloniaTest]
    public async Task F2InTheListStartsRenamingTheSelectedEnvironment()
    {
        await OpenWindowAsync();
        _viewModel.SelectedEnvironment = _viewModel.Environments[0];
        FocusListItem(0);

        _window.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.That(_viewModel.Environments[0].IsEditing, Is.True);
    }

    [AvaloniaTest]
    public async Task DeleteKeyInTheListDeletesTheSelectedEnvironmentAfterConfirmation()
    {
        await OpenWindowAsync();
        _viewModel.SelectedEnvironment = _viewModel.Environments[0];
        FocusListItem(0);

        _window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        await Task.Delay(50);
        Dispatcher.UIThread.RunJobs();

        A.CallTo(() => _environments.DeleteAsync(StagingId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [AvaloniaTest]
    public async Task DeleteKeyInTheNameBoxEditsTheTextAndDeletesNothing()
    {
        await OpenWindowAsync();
        _viewModel.SelectedEnvironment = _viewModel.Environments[0];
        _viewModel.RenameCommand.Execute(null);
        await Task.Delay(50);
        Dispatcher.UIThread.RunJobs();
        var box = (TextBox)_window.FocusManager!.GetFocusedElement()!;

        _window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        await Task.Delay(50);
        Dispatcher.UIThread.RunJobs();

        Assert.That(box.Text, Is.Empty);
        A.CallTo(() => _environments.DeleteAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [AvaloniaTest]
    public async Task EnterInTheNameBoxRenamesTheEnvironment()
    {
        await OpenWindowAsync();
        _viewModel.SelectedEnvironment = _viewModel.Environments[0];
        _viewModel.RenameCommand.Execute(null);
        await Task.Delay(50);
        Dispatcher.UIThread.RunJobs();
        var box = (TextBox)_window.FocusManager!.GetFocusedElement()!;
        box.Text = "QA";

        _window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        await Task.Delay(50);
        Dispatcher.UIThread.RunJobs();

        A.CallTo(() => _environments.RenameAsync(StagingId, "QA", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [AvaloniaTest]
    public async Task LeavingTheNameBoxKeepsTheEdit()
    {
        await OpenWindowAsync();
        _viewModel.SelectedEnvironment = _viewModel.Environments[0];
        _viewModel.RenameCommand.Execute(null);
        await Task.Delay(50);
        Dispatcher.UIThread.RunJobs();
        ((TextBox)_window.FocusManager!.GetFocusedElement()!).Text = "QA";

        FocusListItem(1);
        await Task.Delay(50);
        Dispatcher.UIThread.RunJobs();

        A.CallTo(() => _environments.RenameAsync(StagingId, "QA", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [AvaloniaTest]
    public async Task EscapeInTheNameBoxCancelsTheEdit()
    {
        await OpenWindowAsync();
        _viewModel.SelectedEnvironment = _viewModel.Environments[0];
        _viewModel.RenameCommand.Execute(null);
        await Task.Delay(50);
        Dispatcher.UIThread.RunJobs();
        ((TextBox)_window.FocusManager!.GetFocusedElement()!).Text = "QA";

        _window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Task.Delay(50);
        Dispatcher.UIThread.RunJobs();

        A.CallTo(() => _environments.RenameAsync(A<Guid>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        Assert.That(_viewModel.Environments[0].IsEditing, Is.False);
    }

    [AvaloniaTest]
    public async Task ContextMenuOffersSetCurrentCreateRenameAndDelete()
    {
        await OpenWindowAsync();
        var menu = _window.FindControl<ListBox>("List")!.ContextMenu!;
        menu.Open();
        Dispatcher.UIThread.RunJobs();

        var items = menu.Items.OfType<MenuItem>().ToList();

        Assert.That(items.Select(item => item.Header), Is.EqualTo(ContextMenuHeaders));
        Assert.That(items.Select(item => item.Command), Is.EqualTo(ExpectedMenuCommands()));
    }

    [AvaloniaTest]
    public async Task TheCurrentEnvironmentIsMarkedInTheList()
    {
        await OpenWindowAsync();

        var marks = _window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>()
            .Where(path => path.DataContext is EnvironmentItem && path.IsVisible)
            .Select(path => ((EnvironmentItem)path.DataContext!).Name)
            .ToList();

        Assert.That(marks, Is.EqualTo(MarkedEnvironments));
    }

    private void FocusListItem(int index)
    {
        _window.FindControl<ListBox>("List")!.ContainerFromIndex(index)!.Focus();
        Dispatcher.UIThread.RunJobs();
    }

    private System.Windows.Input.ICommand[] ExpectedMenuCommands()
    {
        return [_viewModel.SetCurrentCommand, _viewModel.CreateCommand, _viewModel.RenameCommand, _viewModel.DeleteCommand];
    }

    private async Task OpenWindowAsync()
    {
        _environments = A.Fake<IEnvironmentService>();
        A.CallTo(() => _environments.ListAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<IReadOnlyList<RequestEnvironment>>.Success(
                [new RequestEnvironment(StagingId, "Staging", []), new RequestEnvironment(ProductionId, "Production", [])])));
        A.CallTo(() => _environments.GetActiveAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<RequestEnvironment?>.Success(new RequestEnvironment(ProductionId, "Production", []))));
        A.CallTo(() => _environments.DeleteAsync(A<Guid>._, A<CancellationToken>._)).Returns(Task.FromResult(Result.Success()));
        A.CallTo(() => _environments.RenameAsync(A<Guid>._, A<string>._, A<CancellationToken>._)).Returns(Task.FromResult(Result.Success()));
        _confirmations = A.Fake<IConfirmationDialogs>();
        A.CallTo(() => _confirmations.ConfirmAsync(A<string>._, A<CancellationToken>._)).Returns(Task.FromResult(true));
        _viewModel = new EnvironmentEditorViewModel(_environments, A.Fake<ISecretPrompt>(), _confirmations);
        await _viewModel.ReloadAsync();
        _window = new EnvironmentEditorWindow { DataContext = _viewModel };
        _window.Show();
        Dispatcher.UIThread.RunJobs();
    }
}
