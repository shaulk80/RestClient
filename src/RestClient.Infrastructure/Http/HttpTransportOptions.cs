namespace RestClient.Infrastructure.Http;

public sealed record HttpTransportOptions(bool FollowRedirects, int MaxRedirects);
