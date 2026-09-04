using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

namespace AIUsage;

[SupportedOSPlatform("macos")]
public sealed class MacPlatform : IPlatform
{
    private const string AgentLabel = "com.aiusage.agent";

    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static string AgentPath =>
        Path.Combine(Home, "Library", "LaunchAgents", AgentLabel + ".plist");

    public string SettingsDirectory =>
        Path.Combine(Home, "Library", "Application Support", "AIUsage");

    public string AutoStartLabel => "Iniciar al abrir sesión";

    public bool AutoStartEnabled => File.Exists(AgentPath);

    public void SetAutoStart(bool enabled)
    {
        try
        {
            if (enabled) WriteAgent();
            else RemoveAgent();
        }
        catch { }
    }

    private static void WriteAgent()
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable)) return;

        Directory.CreateDirectory(Path.GetDirectoryName(AgentPath)!);
        File.WriteAllText(AgentPath, $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
              <key>Label</key><string>{AgentLabel}</string>
              <key>ProgramArguments</key><array><string>{Escape(executable)}</string></array>
              <key>RunAtLoad</key><true/>
            </dict>
            </plist>

            """);

        // launchd ya carga ~/Library/LaunchAgents en el próximo inicio de sesión;
        // bootstrap solo sirve para que surta efecto sin reiniciar sesión.
        Launchctl("bootstrap", $"gui/{Uid()}", AgentPath);
    }

    private static void RemoveAgent()
    {
        if (File.Exists(AgentPath)) Launchctl("bootout", $"gui/{Uid()}/{AgentLabel}");
        File.Delete(AgentPath);
    }

    private static void Launchctl(params string[] arguments)
    {
        try
        {
            var info = new ProcessStartInfo("/bin/launchctl") { UseShellExecute = false, CreateNoWindow = true };
            foreach (var argument in arguments) info.ArgumentList.Add(argument);
            using var process = Process.Start(info);
            process?.WaitForExit(5000);
        }
        catch { }
    }

    private static uint Uid() => (uint)GetUid();

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "getuid")]
    private static extern int GetUid();

    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    public CommandLaunch? FindCodex()
    {
        // Las apps lanzadas desde Finder heredan un PATH mínimo, sin Homebrew ni gestores
        // de versiones de Node. Buscamos también en las rutas habituales de instalación.
        string[] known =
        [
            "/opt/homebrew/bin/codex",
            "/usr/local/bin/codex",
            Path.Combine(Home, ".local", "bin", "codex"),
            Path.Combine(Home, ".npm-global", "bin", "codex"),
            Path.Combine(Home, ".bun", "bin", "codex"),
            Path.Combine(Home, ".volta", "bin", "codex")
        ];
        return ProviderEnvironment.FindCommand("codex", ProviderEnvironment.CodexArguments, known,
            Environment.GetEnvironmentVariable("PATH"), pathExt: "", commandProcessor: null);
    }
}
