using System;
using System.IO;
using System.Linq;
using Xunit;

namespace AIUsage.Tests;

public sealed class ProviderEnvironmentTests
{
    [Fact]
    public void Claude_uses_default_Windows_profile()
    {
        var path = ProviderEnvironment.ClaudeCredentialsPath(null, @"C:\Users\OtherUser");
        Assert.Equal(@"C:\Users\OtherUser\.claude\.credentials.json", path);
    }

    [Fact]
    public void Claude_respects_custom_config_directory()
    {
        var path = ProviderEnvironment.ClaudeCredentialsPath(@"D:\ClaudeProfile", @"C:\Users\Ignored");
        Assert.Equal(@"D:\ClaudeProfile\.credentials.json", path);
    }

    [Fact]
    public void Npm_command_wrapper_runs_through_command_processor()
    {
        var directory = CreateTempDirectory();
        try
        {
            var wrapper = Path.Combine(directory, "codex.cmd");
            File.WriteAllText(wrapper, "@echo off");

            var launch = ProviderEnvironment.FindCommand("codex", ["app-server", "--stdio"], [],
                directory, ".EXE;.CMD", @"C:\Windows\System32\cmd.exe");

            Assert.NotNull(launch);
            Assert.Equal(@"C:\Windows\System32\cmd.exe", launch.FileName);
            Assert.Equal(["/d", "/s", "/c"], launch.Arguments.Take(3));
            Assert.Contains("codex.cmd", launch.Arguments[3], StringComparison.OrdinalIgnoreCase);
            Assert.Contains("app-server", launch.Arguments[3]);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Known_executable_works_without_PATH()
    {
        var directory = CreateTempDirectory();
        try
        {
            var executable = Path.Combine(directory, "codex.exe");
            File.WriteAllBytes(executable, []);

            var launch = ProviderEnvironment.FindCommand("codex", ["app-server", "--stdio"], [executable],
                "", ".EXE;.CMD", "cmd.exe");

            Assert.NotNull(launch);
            Assert.Equal(executable, launch.FileName);
            Assert.Equal(["app-server", "--stdio"], launch.Arguments);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "AIUsageTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
