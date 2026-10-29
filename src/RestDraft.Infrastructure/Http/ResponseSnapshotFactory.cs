using RestDraft.Domain.Collections;
using RestDraft.Domain.Responses;

namespace RestDraft.Infrastructure.Http;

public static class ResponseSnapshotFactory
{
    public static ResponseSnapshot Create(HttpResponseMessage response, ResponseBody body, ResponseTiming timing)
    {
        var headers = response.Headers
            .Concat(response.Content.Headers)
            .Select(header => new KeyValueEntry(header.Key, string.Join(", ", header.Value), true))
            .ToList();

        return new ResponseSnapshot(
            (int)response.StatusCode,
            response.ReasonPhrase ?? string.Empty,
            headers,
            body.Preview,
            body.IsPreviewTruncated,
            body.SizeBytes,
            body.FilePath,
            timing);
    }
}
