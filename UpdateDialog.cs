using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AIUsage;

public static class UpdateDialog
{
    public static Task<bool> ConfirmAsync(Window owner, UpdateRelease release) => ShowAsync(owner, "Actualización disponible",
        $"AI Usage {release.Version} está disponible. Se descargará, verificará y reemplazará al cerrar la aplicación.", "Actualizar", "Más tarde");

    public static async Task ShowInfoAsync(Window owner, string title, string message) =>
        await ShowAsync(owner, title, message, "Aceptar", null);

    private static async Task<bool> ShowAsync(Window owner, string title, string message, string accept, string? cancel)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 440,
            Height = 190,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        var accepted = new Button { Content = accept, MinWidth = 88 };
        accepted.Click += (_, _) => dialog.Close(true);
        buttons.Children.Add(accepted);
        if (cancel is not null)
        {
            var dismissed = new Button { Content = cancel, MinWidth = 88 };
            dismissed.Click += (_, _) => dialog.Close(false);
            buttons.Children.Add(dismissed);
        }
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 18,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                buttons
            }
        };
        return await dialog.ShowDialog<bool>(owner);
    }
}
