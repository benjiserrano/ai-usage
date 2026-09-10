using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace AIUsage;

public partial class App : Application
{
    private UsageCoordinator? coordinator;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // La ventana se oculta en la bandeja al cerrarla, así que el cierre
            // de la última ventana no puede terminar el proceso.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            coordinator = new UsageCoordinator();
            var window = new MainWindow(coordinator);
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => coordinator?.Dispose();
            window.Show();
            _ = coordinator.StartAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
