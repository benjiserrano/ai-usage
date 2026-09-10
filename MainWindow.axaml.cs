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
    private readonly UsageCoordinator coordinator;
    private readonly TrayIcon tray;
    private readonly NativeMenuItem compactItem;
    private readonly NativeMenuItem disconnectedItem;
    private readonly HashSet<string> warned = [];
    private readonly DispatcherTimer saveTimer;
    private bool quitting;
    private bool compactMode;
    private int fullLeft;
    private int fullTop;
    private int? compactLeft;
    private int? compactTop;
    private double fullScale;
    private double compactScale;
    private double lastWidth;
    private bool syncingWidth;

    public MainWindow(UsageCoordinator c)
    {
        InitializeComponent();
        coordinator = c;
        DataContext = c;
        coordinator.SnapshotChanged += OnSnapshot;

        var settings = SettingsStore.Load();
        compactMode = settings.CompactMode;
        fullScale = UiScale.Normalize(settings.FullScale, UiScale.DefaultFull);
        compactScale = UiScale.Normalize(settings.CompactScale, UiScale.DefaultCompact);
        compactLeft = ToNullablePixel(settings.CompactLeft);
        compactTop = ToNullablePixel(settings.CompactTop);
        coordinator.ShowDisconnectedProviders = settings.ShowDisconnectedProviders;
        (fullLeft, fullTop) = GetSafePosition(settings);
        Position = new PixelPoint(fullLeft, fullTop);

        // Arrastrar el borde dispara muchos SizeChanged seguidos: se guarda al reposar.
        saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        // Al soltar el borde se cuadra el ancho con el del contenido escalado (los topes
        // Min/Max dejarían si no una franja transparente) y se persiste el resultado.
        saveTimer.Tick += (_, _) => { SyncWidth(); SaveSettings(); };

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

        disconnectedItem = new NativeMenuItem("Mostrar proveedores desconectados")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = coordinator.ShowDisconnectedProviders
        };
        disconnectedItem.Click += (_, _) =>
        {
            coordinator.ShowDisconnectedProviders = !coordinator.ShowDisconnectedProviders;
            disconnectedItem.IsChecked = coordinator.ShowDisconnectedProviders;
            SaveSettings();
        };
        menu.Add(disconnectedItem);

        menu.Add(MenuItem("Tamaño por defecto", ResetScale));

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
        SizeChanged += OnSizeChanged;
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
        ApplyScale();
        SyncWidth();

        if (compactMode)
            Dispatcher.UIThread.Post(PositionCompact, DispatcherPriority.Loaded);
        else
            Position = new PixelPoint(fullLeft, fullTop);
    }

    private double CurrentScale => compactMode ? compactScale : fullScale;

    /// <summary>Un factor por vista: tipografía, barras y márgenes crecen todos a la vez.</summary>
    private void ApplyScale()
    {
        FullView.LayoutTransform = new ScaleTransform(fullScale, fullScale);
        CompactView.LayoutTransform = new ScaleTransform(compactScale, compactScale);
        MinHeight = compactMode ? 0 : UiScale.DesignMinHeight * fullScale;
    }

    private void SetScale(double scale)
    {
        scale = UiScale.Clamp(scale);
        if (compactMode) compactScale = scale; else fullScale = scale;
        ApplyScale();
        SyncWidth();
    }

    private void ResetScale()
    {
        SetScale(compactMode ? UiScale.DefaultCompact : UiScale.DefaultFull);
        SaveSettings();
    }

    /// <summary>El alto lo decide el contenido, así que el ancho es lo único que fija la escala.</summary>
    private void SyncWidth()
    {
        var width = UiScale.WidthFor(CurrentScale);
        syncingWidth = true;
        Width = width;
        lastWidth = width;
        syncingWidth = false;
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var width = e.NewSize.Width;
        // El redondeo a píxeles físicos mueve el ancho unas décimas: sin banda muerta la
        // escala iría derivando sola en cada arranque.
        if (!syncingWidth && Math.Abs(width - lastWidth) > UiScale.Deadband)
        {
            lastWidth = width;
            if (compactMode) compactScale = UiScale.ScaleFor(width); else fullScale = UiScale.ScaleFor(width);
            ApplyScale();
            saveTimer.Stop();
            saveTimer.Start();
        }

        if (compactMode && IsLoaded && compactLeft is null) PositionCompact();
    }

    private void StartResize(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        e.Handled = true;
        BeginResizeDrag(WindowEdge.East, e);
    }

    private void ZoomWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        SetScale(CurrentScale + (e.Delta.Y > 0 ? UiScale.Step : -UiScale.Step));
        SaveSettings();
        e.Handled = true;
    }

    private void PositionCompact()
    {
        if (!compactMode) return;
        var area = WorkingArea();
        var margin = Scale(8);
        var width = Scale(Bounds.Width);
        var height = Scale(Bounds.Height);
        var left = compactLeft ?? area.X + margin;
        var top = compactTop ?? area.Bottom - height - margin;
        left = Math.Clamp(left, area.X, Math.Max(area.X, area.Right - width));
        top = Math.Clamp(top, area.Y, Math.Max(area.Y, area.Bottom - height));
        compactLeft = left;
        compactTop = top;
        Position = new PixelPoint(left, top);
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

    private void SaveSettings()
    {
        saveTimer.Stop();
        SettingsStore.Save(new(fullLeft, fullTop, compactMode, fullScale, compactScale,
            compactLeft, compactTop, coordinator.ShowDisconnectedProviders));
    }

    private (int Left, int Top) GetSafePosition(WindowSettings settings)
    {
        var width = Scale(UiScale.WidthFor(fullScale));
        var height = Scale(UiScale.DesignMinHeight * fullScale);
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
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        BeginMoveDrag(e);
    }

    private void FinishCompactDrag(object? sender, PointerReleasedEventArgs e)
    {
        if (!compactMode || !e.InitialPressMouseButton.HasFlag(MouseButton.Left)) return;
        compactLeft = Position.X;
        compactTop = Position.Y;
        SaveSettings();
    }

    private static int? ToNullablePixel(double? value) => value is double position && double.IsFinite(position)
        ? (int)Math.Round(position)
        : null;
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
