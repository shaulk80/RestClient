using System.Text;
using RestDraft.Application.Abstractions;
using RestDraft.Domain.Bodies;
using RestDraft.Domain.Results;

namespace RestDraft.Application.Requests;

// Encodes multipart/form-data as in RFC 7578. File parts are read into memory before the request is sent.
public sealed class MultipartBodyEncoder
{
    private const string Crlf = "\r\n";
    private const string FileContentType = "application/octet-stream";

    private readonly IFileReader _files;

    public MultipartBodyEncoder(IFileReader files)
    {
        _files = files;
    }

    public async Task<Result<byte[]>> EncodeAsync(
        IReadOnlyList<MultipartPart> parts,
        IReadOnlyDictionary<string, string> variables,
        string boundary,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        foreach (var part in parts)
        {
            var written = await WritePartAsync(buffer, part, variables, boundary, cancellationToken);
            if (!written.IsSuccess)
            {
                return Result<byte[]>.Failure(written.Error!);
            }
        }

        WriteText(buffer, $"--{boundary}--{Crlf}");
        return Result<byte[]>.Success(buffer.ToArray());
    }

    private async Task<Result> WritePartAsync(
        MemoryStream buffer,
        MultipartPart part,
        IReadOnlyDictionary<string, string> variables,
        string boundary,
        CancellationToken cancellationToken)
    {
        var name = VariableResolver.Resolve(part.Name, variables);
        if (!name.IsSuccess)
        {
            return Result.Failure(name.Error!);
        }

        if (part.Value is not null)
        {
            return WriteValuePart(buffer, boundary, name.Value!, part.Value, variables);
        }

        if (part.FilePath is null)
        {
            return Result.Failure(ResultErrors.Validation($"Multipart part '{part.Name}' needs a value or a file path."));
        }

        return await WriteFilePartAsync(buffer, boundary, name.Value!, part.FilePath, variables, cancellationToken);
    }

    private static Result WriteValuePart(
        MemoryStream buffer,
        string boundary,
        string name,
        string valueTemplate,
        IReadOnlyDictionary<string, string> variables)
    {
        var value = VariableResolver.Resolve(valueTemplate, variables);
        if (!value.IsSuccess)
        {
            return Result.Failure(value.Error!);
        }

        WritePartHeader(buffer, boundary, name, fileName: null);
        WriteText(buffer, value.Value!);
        WriteText(buffer, Crlf);
        return Result.Success();
    }

    private async Task<Result> WriteFilePartAsync(
        MemoryStream buffer,
        string boundary,
        string name,
        string pathTemplate,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        var path = VariableResolver.Resolve(pathTemplate, variables);
        if (!path.IsSuccess)
        {
            return Result.Failure(path.Error!);
        }

        var content = await _files.ReadAllBytesAsync(path.Value!, cancellationToken);
        if (!content.IsSuccess)
        {
            return Result.Failure(content.Error!);
        }

        WritePartHeader(buffer, boundary, name, Path.GetFileName(path.Value!));
        buffer.Write(content.Value!);
        WriteText(buffer, Crlf);
        return Result.Success();
    }

    private static void WritePartHeader(MemoryStream buffer, string boundary, string name, string? fileName)
    {
        var disposition = $"form-data; name=\"{EscapeQuotes(name)}\"";
        WriteText(buffer, $"--{boundary}{Crlf}");
        if (fileName is null)
        {
            WriteText(buffer, $"Content-Disposition: {disposition}{Crlf}{Crlf}");
            return;
        }

        WriteText(buffer, $"Content-Disposition: {disposition}; filename=\"{EscapeQuotes(fileName)}\"{Crlf}");
        WriteText(buffer, $"Content-Type: {FileContentType}{Crlf}{Crlf}");
    }

    // Browsers percent-encode quotes in these names. The receiving server decodes them the same way.
    private static string EscapeQuotes(string text)
    {
        return text.Replace("\"", "%22", StringComparison.Ordinal);
    }

    private static void WriteText(MemoryStream buffer, string text)
    {
        buffer.Write(Encoding.UTF8.GetBytes(text));
    }
}
