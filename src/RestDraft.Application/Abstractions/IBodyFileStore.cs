using RestDraft.Domain.Results;

namespace RestDraft.Application.Abstractions;

// Deletes response bodies that were spilled to disk. Missing files are not an error.
public interface IBodyFileStore
{
    Result Delete(string path);
}
