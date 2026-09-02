using System.Diagnostics; using System.IO;
using System.Text.Json;
namespace AIUsage;
public sealed class JsonLinesClient : IAsyncDisposable
{
    private readonly Process process;
    private readonly StreamWriter input;
    private readonly CancellationTokenSource stop = new();
    private int id;
    public JsonLinesClient(CommandLaunch launch) : this(launch.FileName, launch.Arguments, launch.RawArguments) { }
    public JsonLinesClient(string exe, IEnumerable<string> args, string? rawArguments = null)
    {
        var startInfo = rawArguments is null ? new ProcessStartInfo(exe) : new ProcessStartInfo(exe, rawArguments);
        startInfo.UseShellExecute = false; startInfo.RedirectStandardInput = true; startInfo.RedirectStandardOutput = true; startInfo.RedirectStandardError = true; startInfo.CreateNoWindow = true;
        if (rawArguments is null) foreach (var arg in args) startInfo.ArgumentList.Add(arg);
        process = new Process { StartInfo = startInfo };
        process.Start(); input = process.StandardInput;
    }
    public async Task<JsonDocument?> RequestAsync(string method, object? parameters, CancellationToken ct)
    {
        var requestId = Interlocked.Increment(ref id);
        var request = JsonSerializer.Serialize(new { method, id = requestId, @params = parameters ?? new { } });
        await input.WriteLineAsync(request.AsMemory(), ct); await input.FlushAsync(ct);
        while (!ct.IsCancellationRequested && await process.StandardOutput.ReadLineAsync(ct) is string line)
        {
            try { var doc = JsonDocument.Parse(line); if (doc.RootElement.TryGetProperty("id", out var rid) && rid.GetInt32() == requestId) return doc; doc.Dispose(); } catch (JsonException) { }
        }
        return null;
    }
    public async Task NotifyAsync(string method, object? parameters, CancellationToken ct)
    { var message = JsonSerializer.Serialize(new { method, @params = parameters ?? new { } }); await input.WriteLineAsync(message.AsMemory(), ct); await input.FlushAsync(ct); }
    public ValueTask DisposeAsync() { stop.Cancel(); try { if (!process.HasExited) process.Kill(true); } catch { } process.Dispose(); stop.Dispose(); return ValueTask.CompletedTask; }
}
