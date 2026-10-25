using RestClient.Domain.Results;

namespace RestClient.Application.Abstractions;

public interface IFileReader
{
    Task<Result<byte[]>> ReadAllBytesAsync(string path, CancellationToken cancellationToken);
}
