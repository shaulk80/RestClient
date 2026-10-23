namespace RestClient.Infrastructure.Tests.Persistence;

internal sealed class TestMigrationFolder
{
    public TestMigrationFolder(string root)
    {
        Root = root;
        Directory.CreateDirectory(root);
    }

    public string Root { get; }

    public void AddFile(string folderName, string fileName, string content)
    {
        var folderPath = Path.Combine(Root, folderName);
        Directory.CreateDirectory(folderPath);
        File.WriteAllText(Path.Combine(folderPath, fileName), content);
    }
}
