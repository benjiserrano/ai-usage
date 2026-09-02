using System.Windows;
namespace AIUsage;
public partial class App : System.Windows.Application
{
    private UsageCoordinator? coordinator;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        coordinator = new UsageCoordinator();
        var window = new MainWindow(coordinator);
        MainWindow = window;
        window.Show();
        await coordinator.StartAsync();
    }
    protected override void OnExit(ExitEventArgs e) { coordinator?.Dispose(); base.OnExit(e); }
}
