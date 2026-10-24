namespace RestClient.Domain.Responses;

public sealed record ResponseTiming(double? ConnectMs, double TimeToFirstByteMs, double TotalMs);
