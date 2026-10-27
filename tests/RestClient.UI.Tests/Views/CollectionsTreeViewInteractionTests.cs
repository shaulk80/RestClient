using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using FakeItEasy;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NUnit.Framework;
using RestClient.Domain.Collections;
using RestClient.Domain.Results;
using RestClient.UI.Tests.ViewModels;
using RestClient.UI.ViewModels;
using RestClient.UI.Views;

namespace RestClient.UI.Tests.Views;

[TestFixture]
public class CollectionsTreeViewInteractionTests
{
    // The chevron is 12 px wide. Press near its left edge and move 5 px, which passes the drag
    // threshold but keeps the release inside the chevron.
    private const double PressOffsetFromCenter = -2;
    private const double MovementWhilePressed = 5;

    private Window _window = null!;
    private CollectionsTreeView _view = null!;
    private TreeFixtures _fixtures = null!;

    [TearDown]
    public void TearDown()
    {
        _window.Close();
    }

    [AvaloniaTest]
    public async Task ClickingExpanderOfEachCollectionExpandsIt()
    {
        var sidebar = await OpenSidebarAsync();

        ClickExpander(sidebar.Roots[0]);
        ClickExpander(sidebar.Roots[1]);

        Assert.That(sidebar.Roots[0].IsExpanded, Is.True, "first collection expanded in view model");
        Assert.That(sidebar.Roots[1].IsExpanded, Is.True, "second collection expanded in view model");
        Assert.That(FindItem(sidebar.Roots[0]).IsExpanded, Is.True, "first collection expanded in view");
        Assert.That(FindItem(sidebar.Roots[1]).IsExpanded, Is.True, "second collection expanded in view");
    }

    // The click area of the expander is the whole cell between the left edge of the row and the row text, not only
    // the small chevron: from the first pixels of the row to just before the text, over the full height of the row.
    [TestCase(4, 0.1)]
    [TestCase(4, 0.9)]
    [TestCase(20, 0.5)]
    [TestCase(-4, 0.1)]
    [TestCase(-4, 0.9)]
    [AvaloniaTest]
    public async Task ClickingAnywhereInTheCellLeftOfTheRowTextExpands(double offset, double fractionY)
    {
        var sidebar = await OpenSidebarAsync();
        var item = FindItem(sidebar.Roots[0]);
        var text = item.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == sidebar.Roots[0].Name);
        var rowTop = item.TranslatePoint(new Point(0, 0), _window)!.Value;
        var textLeft = text.TranslatePoint(new Point(0, 0), _window)!.Value.X;
        var x = offset > 0 ? rowTop.X + offset : textLeft + offset;

        Click(new Point(x, rowTop.Y + text.Bounds.Height * 2 * fractionY));

        Assert.That(sidebar.Roots[0].IsExpanded, Is.True);
    }

    [AvaloniaTest]
    public async Task ExpanderButtonSpansFromTheRowEdgeToTheTextAndTheHeightOfTheRow()
    {
        var sidebar = await OpenSidebarAsync();
        var item = FindItem(sidebar.Roots[0]);
        var expander = FindExpander(sidebar.Roots[0]);
        var text = item.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == sidebar.Roots[0].Name);
        var left = expander.TranslatePoint(new Point(0, 0), item)!.Value.X;
        var right = left + expander.Bounds.Width;
        var textLeft = text.TranslatePoint(new Point(0, 0), item)!.Value.X;

        Assert.That(left, Is.LessThanOrEqualTo(2), "starts at the left edge of the row");
        Assert.That(right, Is.GreaterThanOrEqualTo(textLeft - 2), "reaches the text");
        Assert.That(expander.Bounds.Height, Is.GreaterThanOrEqualTo(text.Bounds.Height + 8), "as high as the row");
    }

    [AvaloniaTest]
    public async Task ExpanderClickWithSlightMouseMovementStillExpands()
    {
        var sidebar = await OpenSidebarAsync();
        var expander = FindExpander(sidebar.Roots[0]);
        var center = Center(expander);
        var press = new Point(center.X + PressOffsetFromCenter, center.Y);
        var release = new Point(press.X + MovementWhilePressed, press.Y);

        _window.MouseDown(press, MouseButton.Left);
        _window.MouseMove(release);
        _window.MouseUp(release, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.That(sidebar.Roots[0].IsExpanded, Is.True, "expander expands although the mouse moved past the drag threshold during the press");
    }

    [AvaloniaTest]
    public async Task RightClickOnFolderTextOpensMenuForThatFolder()
    {
        var sidebar = await OpenSidebarAsync();
        ExpandBoth(sidebar);
        var folder = sidebar.Roots[1].Children[0];
        var label = FindItem(folder).GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == folder.Name);

        RightClick(Center(label));

        Assert.That(MenuOpenFor(folder), Is.True, "menu opens for the folder that was right-clicked");
    }

    [AvaloniaTest]
    public async Task RightClickOnBlankRowSpaceOpensMenu()
    {
        var sidebar = await OpenSidebarAsync();
        ExpandBoth(sidebar);
        var folder = sidebar.Roots[1].Children[0];
        var item = FindItem(folder);

        RightClick(item.TranslatePoint(new Point(item.Bounds.Width - 8, item.Bounds.Height / 2), _window)!.Value);

        Assert.That(MenuOpenFor(folder), Is.True, "menu opens when right-clicking blank space in the folder row");
    }

    [AvaloniaTest]
    public async Task RightClickOnAnotherRowWhileMenuIsOpenSelectsItAndMovesMenu()
    {
        var sidebar = await OpenSidebarAsync();
        ExpandBoth(sidebar);
        var first = sidebar.Roots[0].Children[0];
        var second = sidebar.Roots[1].Children[0];
        RightClick(Center(FindItem(first)));

        RightClick(Center(FindItem(second)));

        Assert.That(sidebar.SelectedNode, Is.SameAs(second), "second folder selected");
        Assert.That(MenuOpenFor(second), Is.True, "menu opens for the second folder");
        Assert.That(MenuOpenFor(first), Is.False, "menu for the first folder is closed");
    }

    [AvaloniaTest]
    public async Task LeftClickOutsideMenuClosesIt()
    {
        var sidebar = await OpenSidebarAsync();
        ExpandBoth(sidebar);
        var folder = sidebar.Roots[1].Children[0];
        RightClick(Center(FindItem(folder)));

        Click(Center(FindItem(sidebar.Roots[0])));

        Assert.That(MenuOpenFor(folder), Is.False, "menu closes after a press elsewhere");
    }

    [AvaloniaTest]
    public async Task ClickingMenuItemRunsItsCommand()
    {
        var sidebar = await OpenSidebarAsync();
        ExpandBoth(sidebar);
        var folder = sidebar.Roots[1].Children[0];
        A.CallTo(() => _fixtures.Folders.CreateAsync(folder.Id, A<CancellationToken>._))
            .Returns(Task.FromResult(Result<Guid>.Success(Guid.NewGuid())));
        RightClick(Center(FindItem(folder)));
        var newFolder = _view.GetLogicalDescendants().OfType<MenuItem>().First(item => item.Header as string == "New folder");

        Click(Center(newFolder));

        A.CallTo(() => _fixtures.Folders.CreateAsync(folder.Id, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [AvaloniaTest]
    public async Task NewCollectionMovesFocusToItsRenameBox()
    {
        var sidebar = await OpenSidebarAsync();
        var created = new Collection(Guid.NewGuid(), "New collection", TreeFixtures.CreateFolder(string.Empty));
        A.CallTo(() => _fixtures.Collections.CreateAsync(A<string>._, A<CancellationToken>._))
            .Invokes(() => _fixtures.SetCollections([.. sidebar.Roots.Select(root => new Collection(root.Id, root.Name, TreeFixtures.CreateFolder(string.Empty))), created]))
            .Returns(Task.FromResult(Result<Guid>.Success(created.Id)));

        sidebar.NewCollectionCommand.Execute(null);
        await Task.Delay(50);
        Dispatcher.UIThread.RunJobs();

        var focused = _window.FocusManager?.GetFocusedElement();
        Assert.That(focused, Is.TypeOf<TextBox>());
        Assert.That(((TextBox)focused!).Text, Is.EqualTo("New collection"));
        Assert.That(sidebar.SelectedNode?.IsEditing, Is.True);
    }

    [AvaloniaTest]
    public async Task EscapeClosesTheMenu()
    {
        var sidebar = await OpenSidebarAsync();
        ExpandBoth(sidebar);
        var folder = sidebar.Roots[1].Children[0];
        RightClick(Center(FindItem(folder)));

        _window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.That(MenuOpenFor(folder), Is.False, "menu closes on Escape");
    }

    [AvaloniaTest]
    public async Task ShiftF10OpensTheMenuForTheSelectedNode()
    {
        var sidebar = await OpenSidebarAsync();
        ExpandBoth(sidebar);
        var folder = sidebar.Roots[1].Children[0];
        sidebar.SelectedNode = folder;
        FindItem(folder).Focus();

        _window.KeyPressQwerty(PhysicalKey.F10, RawInputModifiers.Shift);
        Dispatcher.UIThread.RunJobs();

        Assert.That(MenuOpenFor(folder), Is.True, "menu opens for the selected folder");
        Assert.That(_window.FocusManager?.GetFocusedElement(), Is.TypeOf<MenuItem>());
    }

    [AvaloniaTest]
    public async Task F2StartsRenamingTheSelectedNode()
    {
        var sidebar = await OpenSidebarAsync();
        ExpandBoth(sidebar);
        var folder = sidebar.Roots[1].Children[0];
        sidebar.SelectedNode = folder;
        FindItem(folder).Focus();

        _window.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.That(folder.IsEditing, Is.True);
    }

    [AvaloniaTest]
    public async Task ClickingAnotherRowWhileRenamingCommitsTheEdit()
    {
        var sidebar = await OpenSidebarAsync();
        ExpandBoth(sidebar);
        var folder = sidebar.Roots[1].Children[0];
        A.CallTo(() => _fixtures.Folders.RenameAsync(folder.Id, "Renamed", A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Success()));
        sidebar.SelectedNode = folder;
        FindItem(folder).Focus();
        _window.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
        await Task.Delay(50);
        Dispatcher.UIThread.RunJobs();
        var box = (TextBox)_window.FocusManager!.GetFocusedElement()!;
        box.Text = "Renamed";

        Click(Center(FindItem(sidebar.Roots[0])));
        await Task.Delay(50);
        Dispatcher.UIThread.RunJobs();

        A.CallTo(() => _fixtures.Folders.RenameAsync(folder.Id, "Renamed", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [AvaloniaTest]
    public async Task EscapeWhileRenamingDoesNotCommitTheEdit()
    {
        var sidebar = await OpenSidebarAsync();
        ExpandBoth(sidebar);
        var folder = sidebar.Roots[1].Children[0];
        sidebar.SelectedNode = folder;
        FindItem(folder).Focus();
        _window.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
        await Task.Delay(50);
        Dispatcher.UIThread.RunJobs();
        var box = (TextBox)_window.FocusManager!.GetFocusedElement()!;
        box.Text = "Renamed";

        _window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        await Task.Delay(50);
        Dispatcher.UIThread.RunJobs();

        A.CallTo(() => _fixtures.Folders.RenameAsync(A<Guid>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        Assert.That(folder.IsEditing, Is.False);
    }

    [AvaloniaTest]
    public async Task DeleteKeyDeletesTheSelectedNodeAfterConfirmation()
    {
        var sidebar = await OpenSidebarAsync();
        ExpandBoth(sidebar);
        var folder = sidebar.Roots[1].Children[0];
        A.CallTo(() => _fixtures.Folders.DeleteAsync(folder.Id, A<CancellationToken>._))
            .Returns(Task.FromResult(Result.Success()));
        sidebar.SelectedNode = folder;
        FindItem(folder).Focus();

        _window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        await Task.Delay(50);
        Dispatcher.UIThread.RunJobs();

        A.CallTo(() => _fixtures.Folders.DeleteAsync(folder.Id, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [AvaloniaTest]
    public async Task SingleClickOnARequestOpensItWhenTheSettingIsOn()
    {
        var sidebar = await OpenSidebarWithRequestAsync(opensOnSingleClick: true);
        var opened = new List<Guid>();
        sidebar.RequestOpenRequested += opened.Add;

        Click(Center(FindItem(sidebar.Roots[0].Children[0])));

        Assert.That(opened, Is.EqualTo(new[] { sidebar.Roots[0].Children[0].Id }));
    }

    [AvaloniaTest]
    public async Task SingleClickOnARequestOnlySelectsItWhenTheSettingIsOff()
    {
        var sidebar = await OpenSidebarWithRequestAsync(opensOnSingleClick: false);
        var opened = new List<Guid>();
        sidebar.RequestOpenRequested += opened.Add;
        var request = sidebar.Roots[0].Children[0];

        Click(Center(FindItem(request)));

        Assert.That(opened, Is.Empty);
        Assert.That(sidebar.SelectedNode, Is.SameAs(request));
    }

    [AvaloniaTest]
    public async Task SingleClickOnACollectionDoesNotOpenAnythingWhenTheSettingIsOn()
    {
        var sidebar = await OpenSidebarWithRequestAsync(opensOnSingleClick: true);
        var opened = new List<Guid>();
        sidebar.RequestOpenRequested += opened.Add;

        var label = FindItem(sidebar.Roots[0]).GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "Shop");
        Click(Center(label));

        Assert.That(opened, Is.Empty);
    }

    private async Task<CollectionsTreeViewModel> OpenSidebarWithRequestAsync(bool opensOnSingleClick)
    {
        var request = new RequestDefinition { Id = Guid.NewGuid(), Name = "Login" };
        var root = new Folder(Guid.NewGuid(), string.Empty, [], [request]);
        var sidebar = await OpenSidebarAsync([new Collection(Guid.NewGuid(), "Shop", root)]);
        sidebar.OpensOnSingleClick = opensOnSingleClick;
        ClickExpander(sidebar.Roots[0]);
        return sidebar;
    }

    private Task<CollectionsTreeViewModel> OpenSidebarAsync()
    {
        return OpenSidebarAsync([
            new Collection(Guid.NewGuid(), "First", TreeFixtures.CreateFolder(string.Empty, [TreeFixtures.CreateFolder("Alpha")])),
            new Collection(Guid.NewGuid(), "Second", TreeFixtures.CreateFolder(string.Empty, [TreeFixtures.CreateFolder("Beta")])),
        ]);
    }

    private async Task<CollectionsTreeViewModel> OpenSidebarAsync(IReadOnlyList<Collection> collections)
    {
        var fixtures = new TreeFixtures();
        _fixtures = fixtures;
        fixtures.SetCollections(collections);
        var sidebar = fixtures.CreateSidebar();
        await sidebar.ReloadAsync();
        _view = new CollectionsTreeView { DataContext = sidebar };
        _window = new Window
        {
            Width = 400,
            Height = 600,
            Content = _view,
        };
        _window.Show();
        Dispatcher.UIThread.RunJobs();
        return sidebar;
    }

    private void ExpandBoth(CollectionsTreeViewModel sidebar)
    {
        ClickExpander(sidebar.Roots[0]);
        ClickExpander(sidebar.Roots[1]);
    }

    private bool MenuOpenFor(TreeNodeViewModel node)
    {
        var popup = _view.GetLogicalDescendants().OfType<Popup>().Single();
        return popup.IsOpen && ReferenceEquals(popup.DataContext, node);
    }

    private TreeViewItem FindItem(TreeNodeViewModel node)
    {
        return _window.GetVisualDescendants().OfType<TreeViewItem>().First(item => ReferenceEquals(item.DataContext, node));
    }

    private ToggleButton FindExpander(TreeNodeViewModel node)
    {
        return FindItem(node).GetVisualDescendants().OfType<ToggleButton>().First();
    }

    private void ClickExpander(TreeNodeViewModel node)
    {
        Click(Center(FindExpander(node)));
    }

    private void Click(Point point)
    {
        _window.MouseDown(point, MouseButton.Left);
        _window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private void RightClick(Point point)
    {
        _window.MouseDown(point, MouseButton.Right);
        _window.MouseUp(point, MouseButton.Right);
        Dispatcher.UIThread.RunJobs();
    }

    private Point Center(Visual target)
    {
        Dispatcher.UIThread.RunJobs();
        var size = target.Bounds.Size;
        return target.TranslatePoint(new Point(size.Width / 2, size.Height / 2), _window)!.Value;
    }
}
