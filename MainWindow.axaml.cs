using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace AIUsage;

public partial class MainWindow : Window
{
    private const double DefaultWidth = 270;
    private const double DefaultHeight = 150;

    private readonly UsageCoordinator coordinator;
    private readonly TrayIcon tray;
    private readonly NativeMenuItem compactItem;
    private readonly HashSet<string> warned = [];
    private bool quitting;
    private bool compactMode;
    private int fullLeft;
    private int fullTop;

    public MainWindow(UsageCoordinator c)
    {
        InitializeComponent();
        coordinator = c;
        DataContext = c;
        coordinator.SnapshotChanged += OnSnapshot;

        var settings = SettingsStore.Load();
        (fullLeft, fullTop) = GetSafePosition(settings);
        compactMode = settings.CompactMode;
        Position = new PixelPoint(fullLeft, fullTop);

        tray = new TrayIcon { Icon = LoadAppIcon(), ToolTipText = "AI Usage", IsVisible = true };
        var menu = new NativeMenu();
        menu.Add(MenuItem("Mostrar / ocultar", Toggle));
        menu.Add(MenuItem("Actualizar", () => _ = coordinator.RefreshAsync()));

        compactItem = new NativeMenuItem("Vista compacta")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = compactMode
        };
        // No damos por hecho que el menú nativo ya haya invertido IsChecked: el
        // estado se deriva siempre de compactMode y se reescribe al final.
        compactItem.Click += (_, _) => SetCompactMode(!compactMode);
        menu.Add(compactItem);

        var startup = new NativeMenuItem(AppPlatform.Current.AutoStartLabel)
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = AppPlatform.Current.AutoStartEnabled
        };
        startup.Click += (_, _) =>
        {
            AppPlatform.Current.SetAutoStart(!AppPlatform.Current.AutoStartEnabled);
            startup.IsChecked = AppPlatform.Current.AutoStartEnabled;
        };
        menu.Add(startup);

        menu.Add(new NativeMenuItemSeparator());
        menu.Add(MenuItem("Salir", Quit));
        tray.Menu = menu;
        tray.Clicked += (_, _) => Toggle();

        ApplyViewMode();
        Opened += (_, _) => { if (compactMode) PositionCompact(); };
        SizeChanged += (_, _) => { if (compactMode && IsLoaded) PositionCompact(); };
        Screens.Changed += OnScreensChanged;
    }

    private static NativeMenuItem MenuItem(string header, Action action)
    {
        var item = new NativeMenuItem(header);
        item.Click += (_, _) => action();
        return item;
    }

    private void Quit()
    {
        quitting = true;
        tray.IsVisible = false;
        tray.Dispose();
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    private void SetCompactMode(bool enabled)
    {
        if (compactMode != enabled)
        {
            if (enabled) (fullLeft, fullTop) = (Position.X, Position.Y);

            compactMode = enabled;
            ApplyViewMode();
            if (!IsVisible) Show();
            SaveSettings();
        }

        compactItem.IsChecked = compactMode;
    }

    private void ApplyViewMode()
    {
        FullView.IsVisible = !compactMode;
        CompactView.IsVisible = compactMode;
        MinHeight = compactMode ? 0 : DefaultHeight;

        if (compactMode)
            Dispatcher.UIThread.Post(PositionCompact, DispatcherPriority.Loaded);
        else
            Position = new PixelPoint(fullLeft, fullTop);
    }

    private void PositionCompact()
    {
        if (!compactMode) return;
        var area = WorkingArea();
        var margin = Scale(8);
        Position = new PixelPoint(area.X + margin, area.Bottom - Scale(Bounds.Height) - margin);
    }

    private void OnScreensChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(PositionCompact);

    private PixelRect WorkingArea() => Screens.Primary?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);

    /// <summary>Pasa de unidades independientes de dispositivo a los píxeles físicos que usa Position.</summary>
    private int Scale(double value) => (int)Math.Round(value * (Screens.Primary?.Scaling ?? 1.0));

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!compactMode) (fullLeft, fullTop) = (Position.X, Position.Y);
        SaveSettings();

        if (!quitting)
        {
            e.Cancel = true;
            Hide();
            base.OnClosing(e);
            return;
        }

        Screens.Changed -= OnScreensChanged;
        base.OnClosing(e);
    }

    private void SaveSettings() => SettingsStore.Save(new(fullLeft, fullTop, compactMode));

    private (int Left, int Top) GetSafePosition(WindowSettings settings)
    {
        var width = Scale(DefaultWidth);
        var height = Scale(DefaultHeight);
        if (double.IsFinite(settings.Left) && double.IsFinite(settings.Top))
        {
            var saved = new PixelRect(
                (int)Math.Round(settings.Left), (int)Math.Round(settings.Top), width, height);
            if (Screens.All.Any(screen => screen.WorkingArea.Intersects(saved))) return (saved.X, saved.Y);
        }

        var area = WorkingArea();
        return (area.Right - width - Scale(18), area.Y + Scale(18));
    }

    private static WindowIcon? LoadAppIcon()
    {
        try
        {
            return new WindowIcon(new Bitmap(AssetLoader.Open(new Uri("avares://AIUsage/Assets/app-icon.png"))));
        }
        catch { return null; }
    }

    private void OnSnapshot(object? sender, UsageSnapshot snapshot)
    {
        foreach (var window in snapshot.Windows)
            foreach (var threshold in new[] { 25d, 10d })
            {
                var key = $"{snapshot.Provider}:{window.Id}:{threshold}";
                if (window.RemainingPercent > threshold) warned.Remove(key);
                else if (warned.Add(key))
                    AppPlatform.Current.Notify($"{snapshot.Provider}: {window.Label}",
                        $"{window.RemainingPercent:0}% restante");
            }
    }

    private void Toggle()
    {
        if (IsVisible) Hide();
        else { Show(); Activate(); }
    }

    private void HideClick(object? sender, RoutedEventArgs e) => Hide();

    private void DragWindow(object? sender, PointerPressedEventArgs e)
    {
        if (!compactMode && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }
}

public sealed class StateColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is ProviderState state
        ? state switch
        {
            ProviderState.Available => new SolidColorBrush(Color.FromRgb(36, 210, 140)),
            ProviderState.Stale or ProviderState.AuthRequired => new SolidColorBrush(Color.FromRgb(245, 180, 55)),
            ProviderState.Error or ProviderState.RateLimited => new SolidColorBrush(Color.FromRgb(240, 85, 90)),
            _ => new SolidColorBrush(Colors.Gray)
        }
        : new SolidColorBrush(Colors.Gray);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class CompactWindowsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is IEnumerable<QuotaWindow> windows ? windows.Take(2) : Array.Empty<QuotaWindow>();

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class ProviderShortNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value?.ToString() switch
    {
        string name when name.StartsWith("Codex", StringComparison.OrdinalIgnoreCase) => "Codex",
        string name when name.StartsWith("Claude", StringComparison.OrdinalIgnoreCase) => "Claude",
        string name => name,
        _ => ""
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class ResetTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTimeOffset reset) return "";
        var local = reset.ToLocalTime();
        return local.Date == DateTimeOffset.Now.Date ? local.ToString("HH:mm") : local.ToString("dd/MM HH:mm");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
