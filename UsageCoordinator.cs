using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace AIUsage;
public sealed class UsageCoordinator : INotifyPropertyChanged, IDisposable
{
    public ObservableCollection<UsageSnapshot> Snapshots { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<UsageSnapshot>? SnapshotChanged;
    private readonly List<IUsageProvider> providers = [new CodexProvider(), new ClaudeProvider()];
    private CancellationTokenSource? stop;
    public async Task StartAsync()
    {
        stop = new CancellationTokenSource();
        foreach (var p in providers) p.SnapshotChanged += OnSnapshot;
        await Task.WhenAll(providers.Select(p => p.StartAsync(stop.Token)));
        _ = PollAsync(stop.Token);
    }
    public async Task RefreshAsync() { if (stop is null) return; await Task.WhenAll(providers.Select(p => p.RefreshAsync(stop.Token))); }
    private async Task PollAsync(CancellationToken ct) { using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1)); while (await timer.WaitForNextTickAsync(ct)) { try { await RefreshAsync(); } catch (OperationCanceledException) { } catch { } } }
    private void OnSnapshot(object? sender, UsageSnapshot snapshot) { void Update() { var old = Snapshots.FirstOrDefault(x => x.Provider == snapshot.Provider); if (old is not null) Snapshots[Snapshots.IndexOf(old)] = snapshot; else Snapshots.Add(snapshot); PropertyChanged?.Invoke(this, new(nameof(Snapshots))); SnapshotChanged?.Invoke(this, snapshot); } if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) Update(); else Avalonia.Threading.Dispatcher.UIThread.Post(Update); }
    public void Dispose() { stop?.Cancel(); foreach (var p in providers) { p.SnapshotChanged -= OnSnapshot; p.DisposeAsync().AsTask().GetAwaiter().GetResult(); } stop?.Dispose(); }
}
