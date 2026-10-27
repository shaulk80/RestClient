using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FakeItEasy;
using NUnit.Framework;
using RestClient.Application.Abstractions;
using RestClient.Application.Environments;
using RestClient.Application.History;
using RestClient.Application.Secrets;
using RestClient.Application.Settings;
using RestClient.Application.Tabs;
using RestClient.Domain.Bodies;
using RestClient.Domain.Auth;
using RestClient.Domain.Collections;
using RestClient.Domain.Environments;
using RestClient.Domain.History;
using RestClient.Domain.Responses;
using RestClient.Domain.Results;
using RestClient.UI.Controls;
using RestClient.UI.Services;
using RestClient.UI.Tests.ViewModels;
using RestClient.UI.ViewModels;

namespace RestClient.UI.Tests.Views;

// Starts the real main window with fake services, and drives it with the keyboard and mouse.
[TestFixture]
public partial class MainWindowSmokeTests
{
    private TreeFixtures _fixtures = null!;
    private IRequestSender _sender = null!;
    private ITabService _tabService = null!;
    private RequestDefinition _stored = null!;
    private AppSettings? _initialSettings;
    private ISettingsService _settings = null!;
    private MainWindowViewModel _viewModel = null!;
    private MainWindow _window = null!;

    [TearDown]
    public void TearDown()
    {
        _window.Close();
    }

    [AvaloniaTest]
    public void WindowOpensAndRestoresTheStoredTabs()
    {
        OpenWindow();

        A.CallTo(() => _tabService.RestoreAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        Assert.That(_viewModel.TabStrip.Tabs, Has.Count.EqualTo(1));
        Assert.That(_viewModel.TabStrip.SelectedTab?.Name, Is.EqualTo("Stored"));
        Assert.That(_window.GetVisualDescendants().OfType<TextBox>().Any(box => box.Text == _stored.Url), Is.True);
    }

    [AvaloniaTest]
    public void CtrlTOpensNewTab()
    {
        OpenWindow();

        _window.KeyPressQwerty(PhysicalKey.T, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();

        Assert.That(_viewModel.TabStrip.Tabs, Has.Count.EqualTo(2));
        Assert.That(_viewModel.TabStrip.SelectedTab?.Name, Is.EqualTo("New request"));
    }

    [AvaloniaTest]
    public void CtrlF4ClosesTheSelectedTab()
    {
        OpenWindow();

        _window.KeyPressQwerty(PhysicalKey.F4, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();

        Assert.That(_viewModel.TabStrip.Tabs, Is.Empty);
    }

    [AvaloniaTest]
    public void CtrlEnterSendsWhileTheUrlBoxHasFocus()
    {
        OpenWindow();
        FocusUrlBox();

        _window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();

        A.CallTo(() => _sender.SendAsync(A<RequestDefinition>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [AvaloniaTest]
    public void CtrlEnterSendsWhileTheBodyEditorHasFocus()
    {
        OpenWindow();
        var body = _viewModel.TabStrip.SelectedTab!.BodyEditor;
        body.SelectedType = body.Types.Single(choice => choice.Type == BodyType.Raw);
        Dispatcher.UIThread.RunJobs();
        var editor = _window.GetVisualDescendants().OfType<CodeEditor>().First(candidate => candidate.IsEffectivelyVisible);
        editor.TextArea.Focus();

        _window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();

        A.CallTo(() => _sender.SendAsync(A<RequestDefinition>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        Assert.That(editor.Text, Is.Empty);
    }

    [AvaloniaTest]
    public void CtrlSSavesTheSelectedTab()
    {
        OpenWindow();
        FocusUrlBox().Text = "https://example.com/edited";
        Dispatcher.UIThread.RunJobs();
        Assert.That(_viewModel.TabStrip.SelectedTab?.IsDirty, Is.True);

        _window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.Control);
        Dispatcher.UIThread.RunJobs();

        Assert.That(_viewModel.TabStrip.SelectedTab?.IsDirty, Is.False);
    }

    [AvaloniaTest]
    public void SwitchingTabsKeepsCleanTabsClean()
    {
        OpenWindow(
            new RequestDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Form",
                Url = "https://example.com/form",
                Method = RequestMethod.Post,
                Body = new FormUrlEncodedBody([new KeyValueEntry("a", "1", true), new KeyValueEntry("b", "2", false)]),
                Auth = new BearerAuth("token"),
                Headers = [new KeyValueEntry("X-Form", "1", true), new KeyValueEntry("X-Off", "2", false)],
                QueryParams = [new KeyValueEntry("a", "b", false)],
            },
            new RequestDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Multipart",
                Url = "https://example.com/multipart",
                Method = RequestMethod.Put,
                Body = new MultipartBody([new MultipartPart("text", "value", null), new MultipartPart("file", null, "C:\\a.txt")]),
                Auth = new ApiKeyAuth("X-Key", "value", ApiKeyLocation.Query),
            },
            new RequestDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Raw",
                Url = "https://example.com/raw",
                Method = RequestMethod.Patch,
                Body = new RawBody("application/json", "{\"a\": 1}"),
                Auth = new BasicAuth("user", "secret"),
            });
        var tabs = _viewModel.TabStrip.Tabs;

        foreach (var tab in tabs.Concat(tabs.Reverse()).ToList())
        {
            _viewModel.TabStrip.SelectedTab = tab;
            Dispatcher.UIThread.RunJobs();
        }

        Assert.That(tabs.Select(tab => tab.IsDirty), Is.All.False);
        Assert.That(tabs[1].Headers.ToEntries(), Has.Count.EqualTo(2));
        Assert.That(tabs[1].QueryParams.ToEntries(), Is.EqualTo(new[] { new KeyValueEntry("a", "b", false) }));
        Assert.That(tabs[0].Headers.ToEntries(), Is.Empty);
        Assert.That(
            tabs.Select(tab => tab.Method),
            Is.EqualTo(new[] { RequestMethod.Get, RequestMethod.Post, RequestMethod.Put, RequestMethod.Patch }));
        Assert.That(
            tabs.Select(tab => tab.BodyEditor.Type),
            Is.EqualTo(new[] { BodyType.None, BodyType.FormUrlEncoded, BodyType.Multipart, BodyType.Raw }));
        Assert.That(
            tabs.Select(tab => tab.AuthEditor.Type),
            Is.EqualTo(new[] { AuthType.None, AuthType.Bearer, AuthType.ApiKey, AuthType.Basic }));
    }

    [AvaloniaTest]
    public void CloseButtonOfTabClosesIt()
    {
        OpenWindow();
        var button = _window.GetVisualDescendants().OfType<Button>()
            .First(candidate => candidate.Content as string == "×");

        var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), _window)!.Value;

        _window.MouseDown(center, MouseButton.Left);
        _window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.That(_viewModel.TabStrip.Tabs, Is.Empty);
    }

    [AvaloniaTest]
    public void EveryInteractiveControlHasAnAccessibleName()
    {
        OpenWindow();
        var tab = _viewModel.TabStrip.SelectedTab!;
        tab.Headers.AddRowCommand.Execute(null);
        tab.QueryParams.AddRowCommand.Execute(null);
        tab.BodyEditor.Form.AddRowCommand.Execute(null);
        tab.BodyEditor.Multipart.AddRowCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var unnamed = AccessibleNameChecker.FindUnnamed(_window);

        Assert.That(unnamed, Is.Empty);
    }

    [TestCase(1, "Parameter name", "Params (1)")]
    [TestCase(2, "Header name", "Headers (1)")]
    [AvaloniaTest]
    public void HeadersAndParamsTabsShowTheirRows(int tabIndex, string expectedAutomationName, string expectedHeader)
    {
        OpenWindow();
        var tab = _viewModel.TabStrip.SelectedTab!;
        var editor = tabIndex == (int)RequestSection.Params ? tab.QueryParams : tab.Headers;
        editor.AddRowCommand.Execute(null);
        editor.Rows[0].Key = "X-Test";
        var details = _window.GetVisualDescendants().OfType<TabControl>()
            .First(candidate => AutomationProperties.GetName(candidate) == "Request details");

        details.SelectedIndex = tabIndex;
        Dispatcher.UIThread.RunJobs();

        var box = _window.GetVisualDescendants().OfType<TextBox>().First(candidate => candidate.Text == "X-Test");
        Assert.That(AutomationProperties.GetName(box), Is.EqualTo(expectedAutomationName));
        Assert.That(((TabItem)details.Items[tabIndex]!).Header, Is.EqualTo(expectedHeader));
    }

    private static IHistoryService CreateHistory()
    {
        var history = A.Fake<IHistoryService>();
        A.CallTo(() => history.SearchAsync(A<HistoryFilter>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<IReadOnlyList<HistoryEntry>>.Success([])));
        return history;
    }

    private static IEnvironmentService CreateEnvironments()
    {
        var environments = A.Fake<IEnvironmentService>();
        A.CallTo(() => environments.ListAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<IReadOnlyList<RequestEnvironment>>.Success([])));
        A.CallTo(() => environments.GetActiveAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<RequestEnvironment?>.Success(null)));
        A.CallTo(() => environments.SetActiveAsync(A<Guid?>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Success()));
        return environments;
    }

    private static ISecretService CreateSecrets()
    {
        var secrets = A.Fake<ISecretService>();
        A.CallTo(() => secrets.IsSetUpAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(Result<bool>.Success(false)));
        return secrets;
    }

    private TextBox FocusUrlBox()
    {
        var box = _window.GetVisualDescendants().OfType<TextBox>().First(candidate => candidate.Text == _stored.Url);
        box.Focus();
        return box;
    }

    private void OpenWindow(params RequestDefinition[] otherTabs)
    {
        _fixtures = new TreeFixtures();
        _stored = new RequestDefinition { Id = Guid.NewGuid(), Name = "Stored", Url = "https://example.com/stored" };
        A.CallTo(() => _fixtures.Requests.UpdateAsync(A<Guid>._, A<Func<RequestDefinition, RequestDefinition>>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Success()));
        _sender = A.Fake<IRequestSender>();
        A.CallTo(() => _sender.SendAsync(A<RequestDefinition>._, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<ResponseSnapshot>.Failure(new Error(ErrorCategory.Network, "Not sent."))));
        _tabService = TabStripFactory.CreateTabService();
        var restored = new[] { _stored }.Concat(otherTabs)
            .Select(definition => new RestoredTab(definition, IsLinked: true, IsDirty: false, IsSelected: definition == _stored))
            .ToList();
        A.CallTo(() => _tabService.RestoreAsync(A<CancellationToken>._)).Returns(Task.FromResult(
            Result<IReadOnlyList<RestoredTab>>.Success(restored)));

        var services = RequestTabServicesFactory.Create(_sender, _fixtures.Requests, A.Fake<IDestinationPicker>());
        var tabs = TabStripFactory.Create(services, _tabService, TabStripFactory.CreatePrompt(SaveChangesChoice.Discard));
        _settings = SettingsServiceFactory.Create(_initialSettings);
        _viewModel = new MainWindowViewModel(
            _fixtures.CreateSidebar(),
            new HistoryListViewModel(CreateHistory()),
            tabs,
            new EnvironmentSelectorViewModel(CreateEnvironments(), A.Fake<IEnvironmentDialogs>()),
            new SecretStatusViewModel(CreateSecrets(), A.Fake<ISecretPrompt>()),
            new WorkspaceLayoutViewModel(_settings),
            _settings,
            A.Fake<ISettingsDialogs>());
        _window = new MainWindow { DataContext = _viewModel };
        _window.Show();
        Dispatcher.UIThread.RunJobs();
    }
}
