using RestDraft.Domain.Results;

namespace RestDraft.Domain.Collections;

public static class ChildNameRules
{
    private const string NewFolderBaseName = "New folder";

    public static Result ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(ResultErrors.Validation("Name must not be empty."));
        }

        return Result.Success();
    }

    public static Result ValidateChildName(string name, Folder parent, Guid? renamingId)
    {
        var nameResult = ValidateName(name);
        if (!nameResult.IsSuccess)
        {
            return nameResult;
        }

        var trimmed = name.Trim();
        if (ChildNames(parent, renamingId).Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure(ResultErrors.Validation($"A folder or request named '{trimmed}' already exists here."));
        }

        return Result.Success();
    }

    public static string NextAvailableName(string baseName, Folder parent)
    {
        var candidate = baseName;
        for (var suffix = 2; !ValidateChildName(candidate, parent, null).IsSuccess; suffix++)
        {
            candidate = $"{baseName} {suffix}";
        }

        return candidate;
    }

    public static string NewFolderName(Folder parent)
    {
        return NextAvailableName(NewFolderBaseName, parent);
    }

    private static IEnumerable<string> ChildNames(Folder parent, Guid? renamingId)
    {
        return parent.Folders
            .Where(folder => folder.Id != renamingId)
            .Select(folder => folder.Name)
            .Concat(parent.Requests
                .Where(request => request.Id != renamingId)
                .Select(request => request.Name));
    }
}
