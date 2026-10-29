using RestDraft.Domain.Results;

namespace RestDraft.Application.Abstractions;

public interface IFileReader
{
    Task<Result<byte[]>> ReadAllBytesAsync(string path, CancellationToken cancellationToken);
}
