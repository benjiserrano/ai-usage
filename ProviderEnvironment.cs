using System.IO;

namespace AIUsage;

public sealed record CommandLaunch(string FileName, IReadOnlyList<string> Arguments, string? RawArguments = null);

public static class ProviderEnvironment
{
    public static string ClaudeCredentialsPath()
    {
        var configDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        return ClaudeCredentialsPath(configDir, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    public static string ClaudeCredentialsPath(string? configDir, string userProfile)
    {
        var root = string.IsNullOrWhiteSpace(configDir)
            ? Path.Combine(userProfile, ".claude")
            : Environment.ExpandEnvironmentVariables(configDir.Trim().Trim('"'));
        return Path.Combine(root, ".credentials.json");
    }

    public static CommandLaunch? FindCodex()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var known = new[]
        {
            Path.Combine(local, "Programs", "OpenAI", "Codex", "bin", "codex.exe"),
            Path.Combine(roaming, "npm", "codex.cmd")
        };
        return FindCommand("codex", ["app-server", "--stdio"], known,
            Environment.GetEnvironmentVariable("PATH"), Environment.GetEnvironmentVariable("PATHEXT"),
            Environment.GetEnvironmentVariable("ComSpec"));
    }

    public static CommandLaunch? FindCommand(string name, IReadOnlyList<string> arguments,
        IEnumerable<string> knownPaths, string? path, string? pathExt, string? commandProcessor)
    {
        var extensions = Extensions(pathExt);
        foreach (var directory in (path ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var cleanDirectory = Environment.ExpandEnvironmentVariables(directory.Trim('"'));
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(cleanDirectory, name + extension);
                if (File.Exists(candidate)) return BuildLaunch(candidate, arguments, commandProcessor);
            }
        }

        foreach (var candidate in knownPaths)
            if (File.Exists(candidate)) return BuildLaunch(candidate, arguments, commandProcessor);

        return null;
    }

    private static IReadOnlyList<string> Extensions(string? pathExt)
    {
        var values = (pathExt ?? ".EXE;.CMD;.BAT")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.StartsWith('.') ? x : "." + x)
            .ToList();
        if (!values.Contains("", StringComparer.OrdinalIgnoreCase)) values.Add("");
        return values;
    }

    private static CommandLaunch BuildLaunch(string command, IReadOnlyList<string> arguments, string? commandProcessor)
    {
        var extension = Path.GetExtension(command);
        if (!extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".bat", StringComparison.OrdinalIgnoreCase))
            return new(command, arguments);

        var processor = string.IsNullOrWhiteSpace(commandProcessor) ? "cmd.exe" : commandProcessor;
        var commandLine = "\"" + Quote(command) + " " + string.Join(" ", arguments.Select(Quote)) + "\"";
        return new(processor, ["/d", "/s", "/c", commandLine], $"/d /s /c {commandLine}");
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
}
