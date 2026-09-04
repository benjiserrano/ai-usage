using System.IO;
using System.Runtime.Versioning;
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
}
