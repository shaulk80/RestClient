using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FakeItEasy;
using NUnit.Framework;
using RestClient.Application.Settings;
using RestClient.Domain.Bodies;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;
using RestClient.UI.Controls;
using RestClient.UI.ViewModels;
using RestClient.UI.Views;

namespace RestClient.UI.Tests.Views;

// The M7 window: menu, borders, panel layout, page order of the request editor, and the tab shortcuts.
public partial class MainWindowSmokeTests
{
    private static readonly string[] PageHeaders = ["Body", "Params (0)", "Headers (0)", "Authorization"];
    private static readonly string[] MenuTitles = ["_File", "_Edit", "_View", "_Tools", "_Help"];

    [AvaloniaTest]
    public void RequestPagesStartWithTheBodyAndAFreshTabOpensOnIt()
    {
        OpenWindow();

        var details = FindDetails();

        var headers = details.Items.OfType<TabItem>().Select(item => item.Header?.ToString()).ToList();
        Assert.That(headers, Is.EqualTo(PageHeaders));
        Assert.That(details.SelectedIndex, Is.EqualTo((int)RequestSection.Body));
    }

    [AvaloniaTest]
    public void NewRequestFocusesTheBody()
    {
        OpenWindow();
        _viewModel.TabStrip.SelectedTab!.SectionIndex = (int)RequestSection.Headers;

        _viewModel.TabStrip.NewTab();
        _window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var focused = _window.FocusManager?.GetFocusedElement() as Control;
        Assert.That(FindDetails().SelectedIndex, Is.EqualTo((int)RequestSection.Body));
        Assert.That(focused, Is.Not.Null);
        Assert.That(AutomationProperties.GetName(focused!), Is.EqualTo("Body type"));
    }

    [AvaloniaTest]
    public void NewRequestWithARawBodyFocusesTheEditor()
    {
        OpenWindow();
        _viewModel.TabStrip.OpenUnsaved(new RequestDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Raw",
            Body = new RawBody("application/json", "{}"),
        });
        _window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var focused = _window.FocusManager?.GetFocusedElement();

        Assert.That(focused, Is.Not.Null);
        Assert.That(focused!.GetType().Name, Is.EqualTo("TextArea"));
    }

    [AvaloniaTest]
    public void RestoredTabsDoNotTakeTheFocus()
    {
        OpenWindow();
        _window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        var focused = _window.FocusManager?.GetFocusedElement() as Control;

        Assert.That(focused is null || AutomationProperties.GetName(focused) != "Body type", Is.True);
    }

    [AvaloniaTest]
    public void EachTabKeepsItsOwnPage()
    {
        OpenWindow(new RequestDefinition { Id = Guid.NewGuid(), Name = "Other" });
        var tabs = _viewModel.TabStrip.Tabs;
        _viewModel.TabStrip.SelectedTab = tabs[0];
        FindDetails().SelectedIndex = (int)RequestSection.Headers;

        _viewModel.TabStrip.SelectedTab = tabs[1];
        Dispatcher.UIThread.RunJobs();
        var otherPage = FindDetails().SelectedIndex;
        _viewModel.TabStrip.SelectedTab = tabs[0];
        Dispatcher.UIThread.RunJobs();

        Assert.That(otherPage, Is.EqualTo((int)RequestSection.Body));
        Assert.That(FindDetails().SelectedIndex, Is.EqualTo((int)RequestSection.Headers));
        Assert.That(tabs.Select(tab => tab.IsDirty), Is.All.False);
    }

    [AvaloniaTest]
    public void TabHeadersAreOneSizeLargerThanTheTreeText()
    {
        OpenWindow();

        var headers = _window.GetVisualDescendants().OfType<TabItem>().ToList();

        Assert.That(headers, Is.Not.Empty);
        Assert.That(headers.Select(header => header.FontSize), Is.All.EqualTo(_window.FontSize + 1));
    }

    [AvaloniaTest]
    public void TabHeadersHaveABorder()
    {
        OpenWindow();

        var headers = _window.GetVisualDescendants().OfType<TabItem>().ToList();

        Assert.That(headers.Select(header => header.BorderThickness), Is.All.EqualTo(new Thickness(1)));
        Assert.That(headers.Select(header => header.BorderBrush), Is.All.Not.Null);
    }

    [AvaloniaTest]
    public void ResponseIsOnTheRightOfTheRequest()
    {
        OpenWindow();
        var details = _window.GetVisualDescendants().OfType<RequestDetailsView>().Single();
        var response = _window.GetVisualDescendants().OfType<ResponseView>().Single();

        var detailsRight = details.TranslatePoint(new Point(details.Bounds.Width, 0), _window)!.Value;
        var responseLeft = response.TranslatePoint(new Point(0, 0), _window)!.Value;
        var responseRight = response.TranslatePoint(new Point(response.Bounds.Width, 0), _window)!.Value;

        Assert.That(responseLeft.X, Is.GreaterThanOrEqualTo(detailsRight.X));
        Assert.That(responseRight.X, Is.LessThanOrEqualTo(_window.Bounds.Width));
        Assert.That(response.Bounds.Width, Is.GreaterThan(0));
    }

    [AvaloniaTest]
    public void SettingsButtonIsGoneAndTheSettingsAreInTheFileMenu()
    {
        OpenWindow();

        var buttons = _window.GetVisualDescendants().OfType<Button>().Select(button => button.Content as string).ToList();
        var menu = _window.GetVisualDescendants().OfType<Menu>().Single();
        var file = menu.Items.OfType<MenuItem>().First(item => item.Header as string == "_File");

        Assert.That(buttons, Does.Not.Contain("Settings"));
        Assert.That(file.Items.OfType<MenuItem>().Select(item => item.Header), Does.Contain("Se_ttings..."));
    }

    [TestCase("_File", new[] { "_New request", "_Save", "Save _all", "_Close tab", "Se_ttings...", "E_xit" })]
    [TestCase("_Edit", new[] { "Cu_t", "_Copy", "_Paste", "Select _all", "S_end", "Cancel s_end" })]
    [TestCase("_View", new[] { "_Collections panel", "_Response panel", "Environment and _secrets bar", "_Next request tab", "_Previous request tab" })]
    [TestCase("_Tools", new[] { "_Unlock secrets", "Manage _environments..." })]
    [TestCase("_Help", new[] { "_About" })]
    [AvaloniaTest]
    public void MainMenuHasTheExpectedItems(string menuName, string[] expectedItems)
    {
        OpenWindow();
        var menu = _window.GetVisualDescendants().OfType<Menu>().Single();

        var top = menu.Items.OfType<MenuItem>().First(item => item.Header as string == menuName);

        Assert.That(top.Items.OfType<MenuItem>().Select(item => item.Header), Is.EqualTo(expectedItems));
    }

    [TestCase("Save _all", "Ctrl+Shift+S")]
    [TestCase("_Save", "Ctrl+S")]
    [TestCase("_New request", "Ctrl+T")]
    [TestCase("_Close tab", "Ctrl+F4")]
    [TestCase("_Next request tab", "Ctrl+Tab")]
    [TestCase("_Previous request tab", "Ctrl+Shift+Tab")]
    [AvaloniaTest]
    public void MenuItemsShowTheirShortcut(string header, string gesture)
    {
        OpenWindow();
        var item = _window.GetVisualDescendants().OfType<Menu>().Single().GetLogicalDescendants().OfType<MenuItem>()
            .First(candidate => candidate.Header as string == header);

        Assert.That(item.InputGesture?.ToString(), Is.EqualTo(gesture));
    }

    [AvaloniaTest]
    public void ToolsMenuRunsTheSameCommandsAsTheTopBar()
    {
        OpenWindow();
        var menu = _window.GetVisualDescendants().OfType<Menu>().Single();
        var tools = menu.Items.OfType<MenuItem>().First(item => item.Header as string == "_Tools");

        var commands = tools.Items.OfType<MenuItem>().Select(item => item.Command).ToList();

        Assert.That(commands, Is.EqualTo(ExpectedToolsCommands()));
    }

    [AvaloniaTest]
    public void MainWindowIsShownInTheTaskbar()
    {
        OpenWindow();

        Assert.That(_window.ShowInTaskbar, Is.True);
    }

    [AvaloniaTest]
    public void ToolsMenuComesAfterViewAndBeforeHelp()
    {
        OpenWindow();
        var menu = _window.GetVisualDescendants().OfType<Menu>().Single();

        var headers = menu.Items.OfType<MenuItem>().Select(item => item.Header as string).ToList();

        Assert.That(headers, Is.EqualTo(MenuTitles));
    }

    [AvaloniaTest]
    public void CtrlShiftSSavesEveryChangedTab()
    {
        OpenWindow(new RequestDefinition { Id = Guid.NewGuid(), Name = "Other", Url = "https://example.com/other" });
        var tabs = _viewModel.TabStrip.Tabs;
        tabs[0].Url = "https://example.com/first";
        tabs[1].Url = "https://example.com/second";
        A.CallTo(() => _fixtures.Requests.UpdateAsync(A<Guid>._, A<Func<RequestDefinition, RequestDefinition>>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Success()));

        _window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.Control | RawInputModifiers.Shift);
        Dispatcher.UIThread.RunJobs();

        Assert.That(tabs.Select(tab => tab.IsDirty), Is.All.False);
    }

    [AvaloniaTest]
    public void CtrlTabSwitchesRequestTabsInUsedOrderAndNothingElse()
    {
        OpenWindow(
            new RequestDefinition { Id = Guid.NewGuid(), Name = "Second" },
            new RequestDefinition { Id = Guid.NewGuid(), Name = "Third" });
        var tabs = _viewModel.TabStrip.Tabs;
        _viewModel.TabStrip.SelectedTab = tabs[2];
        _viewModel.TabStrip.SelectedTab = tabs[0];
        var sidebar = _window.GetVisualDescendants().OfType<TabControl>()
            .First(candidate => AutomationProperties.GetName(candidate) == "Sidebar");
        var details = FindDetails();
        details.SelectedIndex = (int)RequestSection.Params;
        details.Focus();

        _window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();

        Assert.That(_viewModel.TabStrip.SelectedTab, Is.SameAs(tabs[2]));
        Assert.That(sidebar.SelectedIndex, Is.EqualTo(0));
        Assert.That(FindDetails().SelectedIndex, Is.EqualTo((int)RequestSection.Body));
    }

    [AvaloniaTest]
    public void CtrlTabWalksFurtherWhileCtrlIsHeldAndCommitsOnRelease()
    {
        OpenWindow(
            new RequestDefinition { Id = Guid.NewGuid(), Name = "Second" },
            new RequestDefinition { Id = Guid.NewGuid(), Name = "Third" });
        var tabs = _viewModel.TabStrip.Tabs;
        _viewModel.TabStrip.SelectedTab = tabs[1];
        _viewModel.TabStrip.SelectedTab = tabs[0];

        _window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Control);
        _window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Control);
        var whileHeld = _viewModel.TabStrip.SelectedTab;
        _window.KeyReleaseQwerty(PhysicalKey.ControlLeft, RawInputModifiers.None);
        _window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();

        Assert.That(whileHeld, Is.SameAs(tabs[2]));
        Assert.That(_viewModel.TabStrip.SelectedTab, Is.SameAs(tabs[0]));
    }

    [AvaloniaTest]
    public void ResponseBodyAlwaysShowsAScrollBar()
    {
        OpenWindow();

        var editor = _window.GetVisualDescendants().OfType<CodeEditor>()
            .First(candidate => AutomationProperties.GetName(candidate) == "Response body");

        Assert.That(editor.VerticalScrollBarVisibility, Is.EqualTo(Avalonia.Controls.Primitives.ScrollBarVisibility.Visible));
    }

    [AvaloniaTest]
    public void StoredPanelWidthsAreAppliedAtStart()
    {
        _initialSettings = AppSettings.Default with { SidebarWidth = 300, ResponseWidth = 500 };

        OpenWindow();

        Assert.That(PanelColumn("Panels", 0).ActualWidth, Is.EqualTo(300).Within(1));
        Assert.That(PanelColumn("RequestPanels", 2).ActualWidth, Is.EqualTo(500).Within(1));
    }

    [TestCase(ThemePreference.Dark)]
    [TestCase(ThemePreference.Light)]
    [AvaloniaTest]
    public void StoredThemeIsAppliedToTheApplicationAtStart(ThemePreference theme)
    {
        _initialSettings = AppSettings.Default with { Theme = theme };

        try
        {
            OpenWindow();

            Assert.That(Avalonia.Application.Current!.RequestedThemeVariant, Is.EqualTo(RestClient.UI.Services.ThemeVariants.For(theme)));
        }
        finally
        {
            Avalonia.Application.Current!.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Default;
        }
    }

    [AvaloniaTest]
    public async Task DraggingTheResponseSplitterResizesAndRemembersTheWidth()
    {
        OpenWindow();
        var splitter = _window.GetVisualDescendants().OfType<GridSplitter>()
            .First(candidate => AutomationProperties.GetName(candidate) == "Resize the Response panel");
        var before = PanelColumn("RequestPanels", 2).ActualWidth;
        var start = splitter.TranslatePoint(new Point(splitter.Bounds.Width / 2, splitter.Bounds.Height / 2), _window)!.Value;
        var end = new Point(start.X - 100, start.Y);

        _window.MouseDown(start, MouseButton.Left);
        _window.MouseMove(end);
        _window.MouseUp(end, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        var after = PanelColumn("RequestPanels", 2).ActualWidth;
        var stored = (await _settings.GetAsync(CancellationToken.None)).Value!;
        Assert.That(after, Is.EqualTo(before + 100).Within(2));
        Assert.That(stored.ResponseWidth, Is.EqualTo((int)Math.Round(after)).Within(1));
    }

    [AvaloniaTest]
    public void HidingAPanelFromTheViewMenuGivesItsSpaceBack()
    {
        OpenWindow();

        _viewModel.Layout.IsSidebarVisible = false;
        _viewModel.Layout.IsResponseVisible = false;
        _window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.That(PanelColumn("Panels", 0).ActualWidth, Is.EqualTo(0));
        Assert.That(PanelColumn("RequestPanels", 2).ActualWidth, Is.EqualTo(0));
    }

    // Checking a menu item sets IsChecked on the item itself, so the binding must write it back to the view model.
    [TestCase("_Collections panel")]
    [TestCase("_Response panel")]
    [TestCase("Environment and _secrets bar")]
    [AvaloniaTest]
    public void UncheckingAViewMenuItemHidesThePanel(string header)
    {
        OpenWindow();
        var item = FindViewMenuItem(header);

        item.IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        Assert.That(IsShown(header), Is.False);
    }

    [TestCase("_Collections panel")]
    [TestCase("_Response panel")]
    [TestCase("Environment and _secrets bar")]
    [AvaloniaTest]
    public void CheckingAViewMenuItemAgainShowsThePanel(string header)
    {
        OpenWindow();
        var item = FindViewMenuItem(header);
        item.IsChecked = false;

        item.IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.That(IsShown(header), Is.True);
    }

    [AvaloniaTest]
    public void HidingTheEnvironmentBarRemovesItFromTheWindow()
    {
        OpenWindow();
        var bar = _window.GetVisualDescendants().OfType<ComboBox>()
            .First(candidate => AutomationProperties.GetName(candidate) == "Active environment");

        FindViewMenuItem("Environment and _secrets bar").IsChecked = false;
        _window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.That(bar.IsEffectivelyVisible, Is.False);
    }

    [AvaloniaTest]
    public void StoredVisibilityIsAppliedToTheViewMenuAtStart()
    {
        _initialSettings = AppSettings.Default with { SidebarVisible = false, ResponseVisible = true, TopBarVisible = false };

        OpenWindow();

        Assert.That(FindViewMenuItem("_Collections panel").IsChecked, Is.False);
        Assert.That(FindViewMenuItem("_Response panel").IsChecked, Is.True);
        Assert.That(FindViewMenuItem("Environment and _secrets bar").IsChecked, Is.False);
        Assert.That(PanelColumn("Panels", 0).ActualWidth, Is.EqualTo(0));
    }

    [AvaloniaTest]
    public async Task ClosingTheWindowStoresTheViewMenuState()
    {
        OpenWindow();
        FindViewMenuItem("_Response panel").IsChecked = false;
        FindViewMenuItem("Environment and _secrets bar").IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        _window.Close();
        await Task.Delay(100);
        Dispatcher.UIThread.RunJobs();

        var stored = (await _settings.GetAsync(CancellationToken.None)).Value!;
        Assert.That(stored.SidebarVisible, Is.True);
        Assert.That(stored.ResponseVisible, Is.False);
        Assert.That(stored.TopBarVisible, Is.False);
    }

    [AvaloniaTest]
    public void AllSplittersAreNamedAndKeyboardReachable()
    {
        OpenWindow();

        var splitters = _window.GetVisualDescendants().OfType<GridSplitter>().ToList();

        Assert.That(splitters, Has.Count.EqualTo(3));
        Assert.That(splitters.Select(AutomationProperties.GetName), Is.All.Not.Empty);
        Assert.That(splitters.Select(splitter => splitter.Focusable), Is.All.True);
    }

    private System.Windows.Input.ICommand[] ExpectedToolsCommands()
    {
        return [_viewModel.Secrets.ToggleCommand, _viewModel.Environments.ManageCommand];
    }

    private MenuItem FindViewMenuItem(string header)
    {
        var menu = _window.GetVisualDescendants().OfType<Menu>().Single();
        var view = menu.Items.OfType<MenuItem>().First(item => item.Header as string == "_View");
        return view.Items.OfType<MenuItem>().First(item => item.Header as string == header);
    }

    private bool IsShown(string header)
    {
        return header switch
        {
            "_Collections panel" => _viewModel.Layout.IsSidebarVisible,
            "_Response panel" => _viewModel.Layout.IsResponseVisible,
            _ => _viewModel.Layout.IsTopBarVisible,
        };
    }

    private TabControl FindDetails()
    {
        return _window.GetVisualDescendants().OfType<TabControl>()
            .First(candidate => AutomationProperties.GetName(candidate) == "Request details");
    }

    private ColumnDefinition PanelColumn(string gridName, int index)
    {
        return _window.FindControl<Grid>(gridName)!.ColumnDefinitions[index];
    }
}
