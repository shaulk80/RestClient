namespace RestDraft.Infrastructure.Tests.History;

// A temporary migration folder holding copies of chosen versions of the real scripts.
internal sealed class TestMigrationFolderCopy
{
    public TestMigrationFolderCopy(string root)
    {
        Root = root;
        Directory.CreateDirectory(root);
    }

    public string Root { get; }

    public void CopyVersion(string sourceVersionFolder, string version)
    {
        var target = Path.Combine(Root, version);
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(sourceVersionFolder, "*.sql"))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }
    }

    public void Delete()
    {
        Directory.Delete(Root, recursive: true);
    }
}
