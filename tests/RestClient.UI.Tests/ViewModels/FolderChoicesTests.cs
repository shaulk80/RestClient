using NUnit.Framework;
using RestClient.Domain.Collections;
using RestClient.UI.ViewModels;

namespace RestClient.UI.Tests.ViewModels;

[TestFixture]
public class FolderChoicesTests
{
    private static readonly string[] ExpectedPaths =
    [
        "Shop",
        "Shop / Users",
        "Shop / Users / Admins",
        "Blog",
    ];

    [Test]
    public void FromListsEveryFolderWithItsPath()
    {
        var shop = CreateShop();
        var blog = new Collection(Guid.NewGuid(), "Blog", new Folder(Guid.NewGuid(), string.Empty, [], []));

        var choices = FolderChoices.From([shop, blog]);

        Assert.That(choices.Select(choice => choice.Path), Is.EqualTo(ExpectedPaths));
    }

    [Test]
    public void RootFolderExpandsItsCollectionNode()
    {
        var shop = CreateShop();

        var choices = FolderChoices.From([shop]);

        Assert.That(choices[0].FolderId, Is.EqualTo(shop.Root.Id));
        Assert.That(choices[0].ExpandId, Is.EqualTo(shop.Id));
    }

    [Test]
    public void SubfolderExpandsItself()
    {
        var shop = CreateShop();
        var users = shop.Root.Folders[0];

        var choices = FolderChoices.From([shop]);

        Assert.That(choices[1].FolderId, Is.EqualTo(users.Id));
        Assert.That(choices[1].ExpandId, Is.EqualTo(users.Id));
    }

    private static Collection CreateShop()
    {
        var admins = new Folder(Guid.NewGuid(), "Admins", [], []);
        var users = new Folder(Guid.NewGuid(), "Users", [admins], []);
        var root = new Folder(Guid.NewGuid(), string.Empty, [users], []);
        return new Collection(Guid.NewGuid(), "Shop", root);
    }
}
