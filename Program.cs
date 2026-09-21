using Avalonia;

namespace AIUsage;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (UpdateBootstrapper.TryApply(args)) return;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Lo usa el previsualizador de XAML además del arranque normal.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}
