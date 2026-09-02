using System.Text.Json;

namespace AIUsage;

public sealed class CodexProvider : IUsageProvider
{
    public string Name => "Codex CLI";
    public event EventHandler<UsageSnapshot>? SnapshotChanged;

    private JsonLinesClient? client;
    private bool reconnectRequired;

    public Task StartAsync(CancellationToken ct) => ConnectAndRefreshAsync(ct);

    public async Task RefreshAsync(CancellationToken ct)
    {
        if (client is null || reconnectRequired)
        {
            await DisposeClientAsync();
            await ConnectAndRefreshAsync(ct);
            return;
        }

        await RefreshConnectedAsync(ct);
    }

    private async Task ConnectAndRefreshAsync(CancellationToken ct)
    {
        var launch = ProviderEnvironment.FindCodex();
        if (launch is null)
        {
            Emit(UsageSnapshot.Unknown(Name, ProviderState.NotInstalled, "Instala Codex CLI"));
            return;
        }

        try
        {
            client = new JsonLinesClient(launch);
            using var initialized = await client.RequestAsync("initialize", new
            {
                clientInfo = new { name = "AIUsage", version = "1.0" },
                capabilities = new { }
            }, ct);
            if (!HasResult(initialized, out var initializationError))
                throw new InvalidOperationException(initializationError ?? "Codex no respondió al iniciar");

            await client.NotifyAsync("initialized", null, ct);
            reconnectRequired = false;
            await RefreshConnectedAsync(ct);
        }
        catch (Exception ex)
        {
            reconnectRequired = true;
            Emit(UsageSnapshot.Unknown(Name, IsAuthenticationError(ex.Message) ? ProviderState.AuthRequired : ProviderState.Error,
                IsAuthenticationError(ex.Message) ? "Inicia sesión: codex login" : Safe(ex)));
        }
    }

    private async Task RefreshConnectedAsync(CancellationToken ct)
    {
        if (client is null) return;

        try
        {
            using var accountDoc = await client.RequestAsync("account/read", new { refreshToken = false }, ct);
            if (!TryResult(accountDoc, out var accountResult, out var accountError))
                throw new InvalidOperationException(accountError ?? "No se pudo leer la cuenta Codex");

            var requiresAuth = accountResult.TryGetProperty("requiresOpenaiAuth", out var required) && required.ValueKind == JsonValueKind.True;
            var hasAccount = accountResult.TryGetProperty("account", out var account) && account.ValueKind == JsonValueKind.Object;
            if (requiresAuth && !hasAccount)
            {
                reconnectRequired = true;
                Emit(UsageSnapshot.Unknown(Name, ProviderState.AuthRequired, "Inicia sesión: codex login"));
                return;
            }

            using var doc = await client.RequestAsync("account/rateLimits/read", null, ct);
            if (!TryResult(doc, out var root, out var error))
                throw new InvalidOperationException(error ?? "No se pudieron leer límites Codex");

            JsonElement limits;
            if (root.TryGetProperty("rateLimitsByLimitId", out var byId) && byId.ValueKind == JsonValueKind.Object &&
                byId.TryGetProperty("codex", out var codex) && codex.ValueKind == JsonValueKind.Object)
                limits = codex;
            else if (root.TryGetProperty("rateLimits", out var legacy) && legacy.ValueKind == JsonValueKind.Object)
                limits = legacy;
            else
                throw new InvalidOperationException("Codex no devolvió límites compatibles");

            var windows = new List<QuotaWindow>();
            foreach (var key in new[] { "primary", "secondary" })
            {
                if (!limits.TryGetProperty(key, out var window) || window.ValueKind != JsonValueKind.Object) continue;
                var minutes = window.TryGetProperty("windowDurationMins", out var duration) && duration.ValueKind == JsonValueKind.Number ? duration.GetDouble() : 0;
                var used = window.TryGetProperty("usedPercent", out var usage) && usage.ValueKind == JsonValueKind.Number ? usage.GetDouble() : 100;
                DateTimeOffset? reset = window.TryGetProperty("resetsAt", out var resetValue) && resetValue.ValueKind == JsonValueKind.Number
                    ? DateTimeOffset.FromUnixTimeSeconds(resetValue.GetInt64())
                    : null;
                windows.Add(new(key, minutes > 0 ? UsageMath.Label(TimeSpan.FromMinutes(minutes)) : key,
                    UsageMath.Remaining(used), reset));
            }

            reconnectRequired = false;
            Emit(new(Name, windows.Count > 0 ? ProviderState.Available : ProviderState.Stale, windows,
                DateTimeOffset.UtcNow, windows.Count > 0 ? null : "Codex no devolvió cuotas"));
        }
        catch (Exception ex)
        {
            var auth = IsAuthenticationError(ex.Message);
            reconnectRequired = auth;
            Emit(UsageSnapshot.Unknown(Name, auth ? ProviderState.AuthRequired : ProviderState.Error,
                auth ? "Inicia sesión: codex login" : Safe(ex)));
        }
    }

    private static bool HasResult(JsonDocument? doc, out string? error) => TryResult(doc, out _, out error);

    private static bool TryResult(JsonDocument? doc, out JsonElement result, out string? error)
    {
        result = default;
        error = null;
        if (doc is null)
        {
            error = "respuesta vacía";
            return false;
        }

        if (doc.RootElement.TryGetProperty("result", out result)) return true;
        if (doc.RootElement.TryGetProperty("error", out var rpcError))
        {
            error = rpcError.TryGetProperty("message", out var message) ? message.GetString() : rpcError.ToString();
            return false;
        }

        error = "respuesta Codex inválida";
        return false;
    }

    private void Emit(UsageSnapshot snapshot) => SnapshotChanged?.Invoke(this, snapshot);

    private static bool IsAuthenticationError(string message) =>
        message.Contains("auth", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("login", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("401", StringComparison.OrdinalIgnoreCase);

    private static string Safe(Exception error) => error.Message.Length > 120 ? error.Message[..120] : error.Message;

    private async ValueTask DisposeClientAsync()
    {
        if (client is null) return;
        await client.DisposeAsync();
        client = null;
    }

    public ValueTask DisposeAsync() => DisposeClientAsync();
}
