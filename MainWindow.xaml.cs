using System.ComponentModel; using System.Drawing; using System.Windows; using System.Windows.Data; using Microsoft.Win32; using Forms = System.Windows.Forms;
namespace AIUsage;
public partial class MainWindow : Window
{
    private readonly UsageCoordinator coordinator; private readonly Forms.NotifyIcon tray; private readonly Icon appIcon; private bool quitting; private readonly HashSet<string> warned = [];
    public MainWindow(UsageCoordinator c)
    { InitializeComponent(); coordinator = c; DataContext = c; coordinator.SnapshotChanged += OnSnapshot; appIcon = LoadAppIcon(); tray = new Forms.NotifyIcon { Icon = appIcon, Text = "AI Usage", Visible = true }; var menu = new Forms.ContextMenuStrip(); menu.Items.Add("Mostrar / ocultar", null, (_, _) => Toggle()); menu.Items.Add("Actualizar", null, async (_, _) => await coordinator.RefreshAsync()); var startup = new Forms.ToolStripMenuItem("Iniciar con Windows") { Checked = AutoStartEnabled, CheckOnClick = true }; startup.CheckedChanged += (_, _) => SetAutoStart(startup.Checked); menu.Items.Add(startup); menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add("Salir", null, (_, _) => { quitting = true; tray.Visible = false; tray.Dispose(); appIcon.Dispose(); System.Windows.Application.Current.Shutdown(); }); tray.ContextMenuStrip = menu; tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) Toggle(); }; var settings = SettingsStore.Load(); (Left, Top) = GetSafePosition(settings); Closing += (_, e) => { SettingsStore.Save(new(Left, Top)); if (!quitting) { e.Cancel = true; Hide(); } }; }
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
    private static Icon LoadAppIcon() { try { if (!string.IsNullOrWhiteSpace(Environment.ProcessPath)) return System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath) ?? (System.Drawing.Icon)SystemIcons.Application.Clone(); } catch { } return (System.Drawing.Icon)SystemIcons.Application.Clone(); }
    private static bool AutoStartEnabled { get { try { using var k = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"); return k?.GetValue("AIUsage") is not null; } catch { return false; } } }
    private static void SetAutoStart(bool enabled) { try { using var k = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"); if (enabled) k.SetValue("AIUsage", $"\"{Environment.ProcessPath}\""); else k.DeleteValue("AIUsage", false); } catch { } }
    private void OnSnapshot(object? sender, UsageSnapshot s) { foreach (var w in s.Windows) foreach (var threshold in new[] { 25d, 10d }) { var key = $"{s.Provider}:{w.Id}:{threshold}"; if (w.RemainingPercent > threshold) warned.Remove(key); else if (warned.Add(key)) tray.ShowBalloonTip(4000, $"{s.Provider}: {w.Label}", $"{w.RemainingPercent:0}% restante", Forms.ToolTipIcon.Warning); } }
    private void Toggle() { if (IsVisible) Hide(); else { Show(); Activate(); } } private void HideClick(object sender, RoutedEventArgs e) => Hide(); private void DragWindow(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) DragMove(); }
}
public sealed class StateColorConverter : IValueConverter
{ public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => value is ProviderState s ? s switch { ProviderState.Available => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(36, 210, 140)), ProviderState.Stale or ProviderState.AuthRequired => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 180, 55)), ProviderState.Error or ProviderState.RateLimited => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 85, 90)), _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray) } : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray); public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException(); }
