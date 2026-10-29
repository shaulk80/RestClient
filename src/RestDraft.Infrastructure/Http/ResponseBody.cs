namespace RestDraft.Infrastructure.Http;

public sealed record ResponseBody(string Preview, long SizeBytes, string? FilePath, bool IsPreviewTruncated);
