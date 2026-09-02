using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AIUsage.Tests;

public sealed class CodexLaunchSmokeTests
{
    [Fact]
    public async Task Npm_wrapper_can_start_app_server_when_present()
    {
        var npmDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm");
        var wrapper = Path.Combine(npmDirectory, "codex.cmd");
        if (!File.Exists(wrapper)) return;

        var launch = ProviderEnvironment.FindCommand("codex", ["app-server", "--stdio"], [],
            npmDirectory, ".CMD", Environment.GetEnvironmentVariable("ComSpec"));
        Assert.NotNull(launch);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var client = new JsonLinesClient(launch);
        using var response = await client.RequestAsync("initialize", new
        {
            clientInfo = new { name = "AIUsage.Tests", version = "1.0" },
            capabilities = new { }
        }, timeout.Token);

        Assert.NotNull(response);
        Assert.True(response.RootElement.TryGetProperty("result", out _));
    }
}
