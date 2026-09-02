using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AIUsage;

public sealed class ClaudeProvider : IUsageProvider
{
    public string Name => "Claude Code";
    public event EventHandler<UsageSnapshot>? SnapshotChanged;

    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private DateTimeOffset lastRequest;

    public Task StartAsync(CancellationToken ct) => RefreshAsync(ct);

    public async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            var path = ProviderEnvironment.ClaudeCredentialsPath();
            if (!File.Exists(path))
            {
                lastRequest = default;
                Emit(UsageSnapshot.Unknown(Name, ProviderState.AuthRequired, "Inicia sesión: claude"));
                return;
            }

            using var auth = JsonDocument.Parse(await File.ReadAllTextAsync(path, ct));
            if (!TryAccessToken(auth.RootElement, out var token))
            {
                lastRequest = default;
                Emit(UsageSnapshot.Unknown(Name, ProviderState.AuthRequired, "Inicia sesión: claude"));
                return;
            }

            if ((DateTimeOffset.UtcNow - lastRequest).TotalSeconds < 300) return;
            lastRequest = DateTimeOffset.UtcNow;

            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/api/oauth/usage");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Headers.TryAddWithoutValidation("anthropic-beta", "oauth-2025-04-20");
            using var res = await http.SendAsync(req, ct);
            if ((int)res.StatusCode is 401 or 403)
            {
                lastRequest = default;
                Emit(UsageSnapshot.Unknown(Name, ProviderState.AuthRequired, "Inicia sesión: claude"));
                return;
            }
            if ((int)res.StatusCode == 429)
            {
                Emit(UsageSnapshot.Unknown(Name, ProviderState.RateLimited, "Consulta limitada; reintento automático"));
                return;
            }

            res.EnsureSuccessStatusCode();
            using var data = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var windows = ParseWindows(data.RootElement);
            Emit(new(Name, windows.Count > 0 ? ProviderState.Available : ProviderState.Stale, windows,
                DateTimeOffset.UtcNow, windows.Count > 0 ? null : "Claude no devolvió cuotas"));
        }
        catch (Exception ex)
        {
            Emit(UsageSnapshot.Unknown(Name, ProviderState.Stale, Safe(ex)));
        }
    }

    private static bool TryAccessToken(JsonElement element, out string token)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String &&
                    (property.Name.Equals("accessToken", StringComparison.OrdinalIgnoreCase) ||
                     property.Name.Equals("access_token", StringComparison.OrdinalIgnoreCase)))
                {
                    token = property.Value.GetString()!;
                    return !string.IsNullOrWhiteSpace(token);
                }
            }

            foreach (var property in element.EnumerateObject())
                if (TryAccessToken(property.Value, out token)) return true;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                if (TryAccessToken(item, out token)) return true;
        }

        token = "";
        return false;
    }

    private static List<QuotaWindow> ParseWindows(JsonElement root)
    {
        var list = new List<QuotaWindow>();
        foreach (var property in root.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object ||
                !property.Name.Contains("hour", StringComparison.OrdinalIgnoreCase) &&
                !property.Name.Contains("day", StringComparison.OrdinalIgnoreCase) &&
                !property.Name.Contains("week", StringComparison.OrdinalIgnoreCase)) continue;
            if (!property.Value.TryGetProperty("utilization", out var utilization) || utilization.ValueKind != JsonValueKind.Number) continue;

            DateTimeOffset? reset = null;
            if (property.Value.TryGetProperty("resets_at", out var resetValue))
                reset = resetValue.ValueKind == JsonValueKind.Number
                    ? DateTimeOffset.FromUnixTimeSeconds(resetValue.GetInt64())
                    : resetValue.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(resetValue.GetString(), out var parsed)
                        ? parsed
                        : null;

            list.Add(new(property.Name, Label(property.Name), UsageMath.Remaining(utilization.GetDouble()), reset));
        }
        return list;
    }

    private static string Label(string id) => id.ToLowerInvariant() switch
    {
        "five_hour" => "5h",
        "seven_day" => "7d",
        "seven_day_sonnet" => "7d Sonnet",
        "seven_day_opus" => "7d Opus",
        _ => id.Replace("_", " ")
    };

    private static string Safe(Exception error) => error.Message.Length > 100 ? error.Message[..100] : error.Message;
    private void Emit(UsageSnapshot snapshot) => SnapshotChanged?.Invoke(this, snapshot);

    public ValueTask DisposeAsync()
    {
        http.Dispose();
        return ValueTask.CompletedTask;
    }
}
