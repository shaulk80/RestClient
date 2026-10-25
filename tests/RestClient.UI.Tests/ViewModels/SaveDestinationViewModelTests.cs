using NUnit.Framework;
using RestClient.UI.Services;
using RestClient.UI.ViewModels;

namespace RestClient.UI.Tests.ViewModels;

[TestFixture]
public class SaveDestinationViewModelTests
{
    private static readonly FolderChoice Shop = new(Guid.NewGuid(), Guid.NewGuid(), "Shop");

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
