using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;

namespace AIUsage;

[SupportedOSPlatform("windows")]
public sealed class WindowsPlatform : IPlatform
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "AIUsage";

    public string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AIUsage");

    public string AutoStartLabel => "Iniciar con Windows";

    public bool AutoStartEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(RunValue) is not null;
            }
            catch { return false; }
        }
    }

    public void SetAutoStart(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled) key.SetValue(RunValue, $"\"{Environment.ProcessPath}\"");
            else key.DeleteValue(RunValue, false);
        }
        catch { }
    }

    public CommandLaunch? FindCodex()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string[] known =
        [
            Path.Combine(local, "Programs", "OpenAI", "Codex", "bin", "codex.exe"),
            Path.Combine(roaming, "npm", "codex.cmd")
        ];
        return ProviderEnvironment.FindCommand("codex", ProviderEnvironment.CodexArguments, known,
            Environment.GetEnvironmentVariable("PATH"), Environment.GetEnvironmentVariable("PATHEXT"),
            Environment.GetEnvironmentVariable("ComSpec"));
    }

    public async Task<string?> ReadClaudeCredentialsAsync(CancellationToken ct)
    {
        var path = ProviderEnvironment.ClaudeCredentialsPath();
        if (!File.Exists(path)) return null;
        return await File.ReadAllTextAsync(path, ct);
    }

    public void Notify(string title, string message)
    {
        // El toast se emite a través de powershell.exe para no arrastrar el SDK de
        // WinRT ni un TFM con versión de Windows, que romperían la build de macOS.
        // El coste (un proceso, ~0,5 s) solo se paga al cruzar un umbral de aviso.
        var script = "[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] > $null\n" +
            "[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] > $null\n" +
            "$xml = New-Object Windows.Data.Xml.Dom.XmlDocument\n" +
            $"$xml.LoadXml('{PowerShellLiteral(ToastXml(title, message))}')\n" +
            "$toast = New-Object Windows.UI.Notifications.ToastNotification $xml\n" +
            "[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('Microsoft.PowerShell').Show($toast)";

        try
        {
            var info = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            info.ArgumentList.Add("-NoProfile");
            info.ArgumentList.Add("-NonInteractive");
            info.ArgumentList.Add("-EncodedCommand");
            info.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
            Process.Start(info);
        }
        catch { }
    }

    private static string ToastXml(string title, string message) =>
        "<toast><visual><binding template=\"ToastText02\">" +
        $"<text id=\"1\">{XmlText(title)}</text><text id=\"2\">{XmlText(message)}</text>" +
        "</binding></visual></toast>";

    private static string XmlText(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string PowerShellLiteral(string value) => value.Replace("'", "''");
}
