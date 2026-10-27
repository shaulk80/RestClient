using NUnit.Framework;
using RestClient.Domain.Collections;
using RestClient.UI.Services;
using RestClient.UI.ViewModels;

namespace RestClient.UI.Tests.ViewModels;

[TestFixture]
public class SaveDestinationViewModelTests
{
    private static readonly Folder ShopFolder = new(
        Guid.NewGuid(),
        string.Empty,
        [new Folder(Guid.NewGuid(), "Users", [], [])],
        [new RequestDefinition { Id = Guid.NewGuid(), Name = "Login", Url = "https://example.com" }]);

    private static readonly FolderChoice Shop = new(ShopFolder.Id, Guid.NewGuid(), "Shop", ShopFolder);

    [Test]
    public void FirstFolderIsSelectedByDefault()
    {
        var viewModel = new SaveDestinationViewModel("Login", [Shop]);

        Assert.That(viewModel.SelectedFolder, Is.EqualTo(Shop));
        Assert.That(viewModel.CanSave, Is.True);
    }

    [Test]
    public void DestinationUsesSelectedFolderAndTrimmedName()
    {
        var viewModel = new SaveDestinationViewModel("  Login  ", [Shop]);

        Assert.That(viewModel.Destination, Is.EqualTo(new SaveDestination(Shop.FolderId, Shop.ExpandId, "Login")));
    }

    [TestCase("Login", "'Login' is already used in this folder. The request will be saved as 'Login 2'.")]
    [TestCase("  login  ", "'login' is already used in this folder. The request will be saved as 'login 2'.")]
    [TestCase("Users", "'Users' is already used in this folder. The request will be saved as 'Users 2'.")]
    [TestCase("Logout", "")]
    [TestCase("   ", "")]
    public void WarnsWhenTheNameIsTakenInTheSelectedFolder(string name, string expectedWarning)
    {
        var viewModel = new SaveDestinationViewModel(name, [Shop]);

        Assert.That(viewModel.NameWarning, Is.EqualTo(expectedWarning));
    }

    [Test]
    public void WarningFollowsTheNameAndTheFolder()
    {
        var empty = new Folder(Guid.NewGuid(), string.Empty, [], []);
        var blog = new FolderChoice(empty.Id, Guid.NewGuid(), "Blog", empty);
        var viewModel = new SaveDestinationViewModel("Login", [Shop, blog]);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.SelectedFolder = blog;
        viewModel.SelectedFolder = Shop;
        viewModel.Name = "Other";

        Assert.That(changed.Count(name => name == nameof(SaveDestinationViewModel.NameWarning)), Is.EqualTo(3));
        Assert.That(viewModel.NameWarning, Is.Empty);
    }

    [TestCase("")]
    [TestCase("   ")]
    public void NameMustNotBeBlank(string name)
    {
        var viewModel = new SaveDestinationViewModel(name, [Shop]);

        Assert.That(viewModel.CanSave, Is.False);
        Assert.That(viewModel.Destination, Is.Null);
    }

    [Test]
    public void SaveIsNotPossibleWithoutFolder()
    {
        var viewModel = new SaveDestinationViewModel("Login", []);

        Assert.That(viewModel.CanSave, Is.False);
        Assert.That(viewModel.Destination, Is.Null);
    }

    [Test]
    public void ChangingNameUpdatesCanSave()
    {
        var viewModel = new SaveDestinationViewModel("Login", [Shop]);

        viewModel.Name = string.Empty;

        Assert.That(viewModel.CanSave, Is.False);
    }
}
