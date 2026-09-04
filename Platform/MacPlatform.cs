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

    // Nombre del servicio bajo el que Claude Code guarda su sesión en el llavero.
    private const string KeychainService = "Claude Code-credentials";

    public async Task<string?> ReadClaudeCredentialsAsync(CancellationToken ct)
    {
        // Un CLAUDE_CONFIG_DIR propio, o una instalación que aún use fichero,
        // mandan sobre el llavero.
        var path = ProviderEnvironment.ClaudeCredentialsPath();
        if (File.Exists(path)) return await File.ReadAllTextAsync(path, ct);

        return await ReadKeychainAsync(ct);
    }

    private static async Task<string?> ReadKeychainAsync(CancellationToken ct)
    {
        try
        {
            var info = new ProcessStartInfo("/usr/bin/security")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            info.ArgumentList.Add("find-generic-password");
            info.ArgumentList.Add("-s");
            info.ArgumentList.Add(KeychainService);
            info.ArgumentList.Add("-w");

            using var process = Process.Start(info);
            if (process is null) return null;

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            // Código 44 significa "no existe esa entrada": no hay sesión iniciada.
            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) ? output.Trim() : null;
        }
        catch { return null; }
    }

    public void Notify(string title, string message)
    {
        try
        {
            var info = new ProcessStartInfo("/usr/bin/osascript") { UseShellExecute = false, CreateNoWindow = true };
            info.ArgumentList.Add("-e");
            info.ArgumentList.Add(
                $"display notification \"{AppleScriptLiteral(message)}\" with title \"{AppleScriptLiteral(title)}\"");
            Process.Start(info);
        }
        catch { }
    }

    private static string AppleScriptLiteral(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

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
