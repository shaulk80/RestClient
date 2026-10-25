using RestClient.Domain.Collections;
using RestClient.Domain.Results;

namespace RestClient.Application.Requests;

public static class KeyValueResolver
{
    public static Result<IReadOnlyList<KeyValueEntry>> Resolve(
        IEnumerable<KeyValueEntry> entries,
        IReadOnlyDictionary<string, string> variables)
    {
        var resolved = new List<KeyValueEntry>();
        foreach (var entry in entries.Where(candidate => candidate.IsEnabled))
        {
            var resolvedEntry = ResolveEntry(entry, variables);
            if (!resolvedEntry.IsSuccess)
            {
                return Result<IReadOnlyList<KeyValueEntry>>.Failure(resolvedEntry.Error!);
            }

            resolved.Add(resolvedEntry.Value!);
        }

        return Result<IReadOnlyList<KeyValueEntry>>.Success(resolved);
    }

    private static Result<KeyValueEntry> ResolveEntry(KeyValueEntry entry, IReadOnlyDictionary<string, string> variables)
    {
        var key = VariableResolver.Resolve(entry.Key, variables);
        if (!key.IsSuccess)
        {
            return Result<KeyValueEntry>.Failure(key.Error!);
        }

        var value = VariableResolver.Resolve(entry.Value, variables);
        if (!value.IsSuccess)
        {
            return Result<KeyValueEntry>.Failure(value.Error!);
        }

        return Result<KeyValueEntry>.Success(new KeyValueEntry(key.Value!, value.Value!, true));
    }
}
