namespace AIUsage;
public enum ProviderState { Available, Stale, AuthRequired, RateLimited, NotInstalled, Error }
public sealed record QuotaWindow(string Id, string Label, double RemainingPercent, DateTimeOffset? ResetsAt);
public sealed record UsageSnapshot(string Provider, ProviderState State, IReadOnlyList<QuotaWindow> Windows, DateTimeOffset UpdatedAt, string? Message = null)
{
    public static UsageSnapshot Unknown(string provider, ProviderState state, string? message = null) => new(provider, state, [], DateTimeOffset.UtcNow, message);
}
