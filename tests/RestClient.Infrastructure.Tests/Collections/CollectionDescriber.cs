using RestClient.Domain.Auth;
using RestClient.Domain.Bodies;
using RestClient.Domain.Collections;

namespace RestClient.Infrastructure.Tests.Collections;

// Turns a tree into one line per node, so two trees can be compared without record equality
// (records compare their lists by reference).
internal static class CollectionDescriber
{
    public static IReadOnlyList<string> Describe(IReadOnlyList<Collection> collections)
    {
        return collections.SelectMany(collection => DescribeFolder($"collection:{collection.Name}", collection.Root)).ToList();
    }

    private static IEnumerable<string> DescribeFolder(string path, Folder folder)
    {
        return [
            $"folder:{path}",
            .. folder.Requests.Select(request => $"request:{path}/{request.Name} {Describe(request)}"),
            .. folder.Folders.SelectMany(child => DescribeFolder($"{path}/{child.Name}", child)),
        ];
    }

    private static string Describe(RequestDefinition request)
    {
        return $"id={request.Id} method={request.Method} url={request.Url} query=[{Describe(request.QueryParams)}] " +
               $"headers=[{Describe(request.Headers)}] body={Describe(request.Body)} auth={Describe(request.Auth)}";
    }

    private static string Describe(IReadOnlyList<KeyValueEntry> entries)
    {
        return string.Join(";", entries.Select(entry => $"{entry.Key}={entry.Value}:{entry.IsEnabled}"));
    }

    private static string Describe(BodyDefinition body)
    {
        return body switch
        {
            RawBody raw => $"raw({raw.ContentType}|{raw.Text})",
            FormUrlEncodedBody form => $"form({Describe(form.Fields)})",
            MultipartBody multipart => $"multipart({string.Join(";", multipart.Parts.Select(DescribePart))})",
            _ => "none",
        };
    }

    private static string DescribePart(MultipartPart part)
    {
        return $"{part.Name}={part.Value ?? string.Empty}@{part.FilePath ?? string.Empty}";
    }

    private static string Describe(AuthDefinition auth)
    {
        return auth switch
        {
            BasicAuth basic => $"basic({basic.Username}|{basic.Password})",
            BearerAuth bearer => $"bearer({bearer.Token})",
            ApiKeyAuth apiKey => $"apikey({apiKey.Key}|{apiKey.Value}|{apiKey.Location})",
            _ => "none",
        };
    }
}
