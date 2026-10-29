using NUnit.Framework;
using RestDraft.Domain.Auth;
using RestDraft.Domain.Bodies;
using RestDraft.Domain.Collections;
using RestDraft.Domain.Responses;

namespace RestDraft.Domain.Tests.Records;

[TestFixture]
public class RecordEqualityTests
{
    private static readonly IReadOnlyList<KeyValueEntry> Headers = [new KeyValueEntry("Accept", "text/plain", true)];

    public static IEnumerable<TestCaseData> EqualPairs()
    {
        yield return new TestCaseData(new KeyValueEntry("k", "v", true), new KeyValueEntry("k", "v", true));
        yield return new TestCaseData(new NoBody(), new NoBody());
        yield return new TestCaseData(new RawBody("text/plain", "x"), new RawBody("text/plain", "x"));
        yield return new TestCaseData(new FormUrlEncodedBody(Headers), new FormUrlEncodedBody(Headers));
        yield return new TestCaseData(new MultipartPart("file", null, "C:\\a.txt"), new MultipartPart("file", null, "C:\\a.txt"));
        yield return new TestCaseData(new NoAuth(), new NoAuth());
        yield return new TestCaseData(new BasicAuth("user", "secret"), new BasicAuth("user", "secret"));
        yield return new TestCaseData(new BearerAuth("token"), new BearerAuth("token"));
        yield return new TestCaseData(new ApiKeyAuth("X-Key", "v", ApiKeyLocation.Header), new ApiKeyAuth("X-Key", "v", ApiKeyLocation.Header));
        yield return new TestCaseData(new ResponseTiming(1.5, 2, 3), new ResponseTiming(1.5, 2, 3));
        yield return new TestCaseData(CreateSnapshot(200, true), CreateSnapshot(200, true));
    }

    public static IEnumerable<TestCaseData> DifferentPairs()
    {
        yield return new TestCaseData(new KeyValueEntry("k", "v", true), new KeyValueEntry("k", "v", false));
        yield return new TestCaseData(new RawBody("text/plain", "x"), new RawBody("text/plain", "y"));
        yield return new TestCaseData(new BasicAuth("user", "secret"), new BasicAuth("user", "other"));
        yield return new TestCaseData(new BearerAuth("token"), new BearerAuth("other"));
        yield return new TestCaseData(new ApiKeyAuth("X-Key", "v", ApiKeyLocation.Header), new ApiKeyAuth("X-Key", "v", ApiKeyLocation.Query));
        yield return new TestCaseData(new ResponseTiming(1.5, 2, 3), new ResponseTiming(null, 2, 3));
        yield return new TestCaseData(CreateSnapshot(200, true), CreateSnapshot(404, true));
        yield return new TestCaseData(CreateSnapshot(200, true), CreateSnapshot(200, false));
    }

    [TestCaseSource(nameof(EqualPairs))]
    public void RecordsWithSameValuesAreEqual(object first, object second)
    {
        Assert.That(first, Is.EqualTo(second));
    }

    [TestCaseSource(nameof(DifferentPairs))]
    public void RecordsWithDifferentValuesAreNotEqual(object first, object second)
    {
        Assert.That(first, Is.Not.EqualTo(second));
    }

    private static ResponseSnapshot CreateSnapshot(int statusCode, bool isPreviewTruncated)
    {
        return new ResponseSnapshot(
            statusCode,
            "OK",
            Headers,
            "body",
            isPreviewTruncated,
            4,
            null,
            new ResponseTiming(null, 1, 2));
    }
}
