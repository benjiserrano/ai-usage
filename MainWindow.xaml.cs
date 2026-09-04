using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace AIUsage;

public partial class MainWindow : Window
{
    private readonly UsageCoordinator coordinator;
    private readonly Forms.NotifyIcon tray;
    private readonly Icon appIcon;
    private readonly Forms.ToolStripMenuItem compactItem;
    private readonly HashSet<string> warned = [];
    private bool quitting;
    private bool compactMode;
    private double fullLeft;
    private double fullTop;

    public MainWindow(UsageCoordinator c)
    {
        InitializeComponent();
        coordinator = c;
        DataContext = c;
        coordinator.SnapshotChanged += OnSnapshot;

        var settings = SettingsStore.Load();
        (fullLeft, fullTop) = GetSafePosition(settings);
        compactMode = settings.CompactMode;
        Left = fullLeft;
        Top = fullTop;

        appIcon = LoadAppIcon();
        tray = new Forms.NotifyIcon { Icon = appIcon, Text = "AI Usage", Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Mostrar / ocultar", null, (_, _) => Toggle());
        menu.Items.Add("Actualizar", null, async (_, _) => await coordinator.RefreshAsync());
        compactItem = new Forms.ToolStripMenuItem("Vista compacta") { Checked = compactMode, CheckOnClick = true };
        compactItem.CheckedChanged += (_, _) => SetCompactMode(compactItem.Checked);
        menu.Items.Add(compactItem);
        var startup = new Forms.ToolStripMenuItem(AppPlatform.Current.AutoStartLabel) { Checked = AppPlatform.Current.AutoStartEnabled, CheckOnClick = true };
        startup.CheckedChanged += (_, _) => AppPlatform.Current.SetAutoStart(startup.Checked);
        menu.Items.Add(startup);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) =>
        {
            quitting = true;
            tray.Visible = false;
            tray.Dispose();
            appIcon.Dispose();
            System.Windows.Application.Current.Shutdown();
        });
        tray.ContextMenuStrip = menu;
        tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) Toggle(); };

        ApplyViewMode();
        Loaded += (_, _) => { if (compactMode) PositionCompact(); };
        SizeChanged += (_, _) => { if (compactMode && IsLoaded) PositionCompact(); };
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        Closing += OnClosing;
    }

    private void SetCompactMode(bool enabled)
    {
        if (compactMode == enabled) return;
        if (enabled)
        {
            fullLeft = Left;
            fullTop = Top;
        }

        compactMode = enabled;
        ApplyViewMode();
        if (!IsVisible) Show();
        SaveSettings();
    }

    private void ApplyViewMode()
    {
        FullView.Visibility = compactMode ? Visibility.Collapsed : Visibility.Visible;
        CompactView.Visibility = compactMode ? Visibility.Visible : Visibility.Collapsed;
        MinHeight = compactMode ? 0 : 150;

        if (compactMode)
            Dispatcher.BeginInvoke(PositionCompact, DispatcherPriority.Loaded);
        else
        {
            Left = fullLeft;
            Top = fullTop;
        }
    }

    private void PositionCompact()
    {
        if (!compactMode) return;
        var area = Forms.Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
        Left = area.Left + 8;
        Top = area.Bottom - ActualHeight - 8;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(PositionCompact);
    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) => Dispatcher.BeginInvoke(PositionCompact);

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!compactMode)
        {
            fullLeft = Left;
            fullTop = Top;
        }
        SaveSettings();

        if (!quitting)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private void SaveSettings() => SettingsStore.Save(new(fullLeft, fullTop, compactMode));

    private static (double Left, double Top) GetSafePosition(WindowSettings settings)
    {
        var width = 270d;
        var height = 150d;
        var valid = double.IsFinite(settings.Left) && double.IsFinite(settings.Top) &&
            Forms.Screen.AllScreens.Any(screen =>
                screen.WorkingArea.IntersectsWith(new System.Drawing.Rectangle(
                    (int)Math.Round(settings.Left), (int)Math.Round(settings.Top),
                    (int)width, (int)height)));
        if (valid) return (settings.Left, settings.Top);

        var area = Forms.Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
        return (area.Right - width - 18, area.Top + 18);
    }

    private static Icon LoadAppIcon()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
                return System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath) ??
                    (System.Drawing.Icon)SystemIcons.Application.Clone();
        }
        catch { }
        return (System.Drawing.Icon)SystemIcons.Application.Clone();
    }

    private void OnSnapshot(object? sender, UsageSnapshot snapshot)
    {
        foreach (var window in snapshot.Windows)
            foreach (var threshold in new[] { 25d, 10d })
            {
                var key = $"{snapshot.Provider}:{window.Id}:{threshold}";
                if (window.RemainingPercent > threshold) warned.Remove(key);
                else if (warned.Add(key))
                    tray.ShowBalloonTip(4000, $"{snapshot.Provider}: {window.Label}",
                        $"{window.RemainingPercent:0}% restante", Forms.ToolTipIcon.Warning);
            }
    }

    private void Toggle()
    {
        if (IsVisible) Hide();
        else { Show(); Activate(); }
    }

    private void HideClick(object sender, RoutedEventArgs e) => Hide();

    private void DragWindow(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!compactMode && e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove();
    }
}

public sealed class StateColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is ProviderState state
        ? state switch
        {
            ProviderState.Available => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(36, 210, 140)),
            ProviderState.Stale or ProviderState.AuthRequired => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 180, 55)),
            ProviderState.Error or ProviderState.RateLimited => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 85, 90)),
            _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray)
        }
        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class CompactWindowsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is IEnumerable<QuotaWindow> windows ? windows.Take(2) : Array.Empty<QuotaWindow>();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class ProviderShortNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value?.ToString() switch
    {
        string name when name.StartsWith("Codex", StringComparison.OrdinalIgnoreCase) => "Codex",
        string name when name.StartsWith("Claude", StringComparison.OrdinalIgnoreCase) => "Claude",
        string name => name,
        _ => ""
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class ResetTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTimeOffset reset) return "";
        var local = reset.ToLocalTime();
        return local.Date == DateTimeOffset.Now.Date ? local.ToString("HH:mm") : local.ToString("dd/MM HH:mm");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
