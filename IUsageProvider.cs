namespace AIUsage;
public interface IUsageProvider : IAsyncDisposable
{
    string Name { get; }
    event EventHandler<UsageSnapshot>? SnapshotChanged;
    Task StartAsync(CancellationToken cancellationToken);
    Task RefreshAsync(CancellationToken cancellationToken);
}
