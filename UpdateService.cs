using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIUsage;

public enum UpdateStatus { None, Available, Unavailable }

public sealed record UpdateRelease(Version Version, string Tag, string DownloadUrl, string Sha256Url);
public sealed record UpdateCheck(UpdateStatus Status, UpdateRelease? Release = null, string? Message = null);

public static class UpdateService
{
    private const string Repository = "benjiserrano/ai-usage";
    private static readonly HttpClient Client = CreateClient();

    public static async Task<UpdateCheck> CheckAsync()
    {
        if (!OperatingSystem.IsWindows()) return new(UpdateStatus.Unavailable, Message: "Actualización automática disponible solo en Windows.");

        try
        {
            using var response = await Client.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest");
            if (!response.IsSuccessStatusCode) return new(UpdateStatus.Unavailable, Message: "No se pudo consultar GitHub Releases.");

            await using var stream = await response.Content.ReadAsStreamAsync();
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream);
            if (release?.TagName is not { } tag || !Version.TryParse(tag.TrimStart('v', 'V'), out var version))
                return new(UpdateStatus.Unavailable, Message: "La release publicada no tiene una versión válida.");

            var installed = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0);
            if (version <= installed) return new(UpdateStatus.None);

            var executable = $"AIUsage-{tag}-win-x64.exe";
            var hash = executable + ".sha256";
            var assets = release.Assets ?? [];
            var executableUrl = assets.FirstOrDefault(asset => asset.Name == executable)?.BrowserDownloadUrl;
            var hashUrl = assets.FirstOrDefault(asset => asset.Name == hash)?.BrowserDownloadUrl;
            if (executableUrl is null || hashUrl is null)
                return new(UpdateStatus.Unavailable, Message: "La release no contiene instalador portable y SHA-256.");

            return new(UpdateStatus.Available, new UpdateRelease(version, tag, executableUrl, hashUrl));
        }
        catch (HttpRequestException) { return new(UpdateStatus.Unavailable, Message: "No hay conexión con GitHub Releases."); }
        catch (JsonException) { return new(UpdateStatus.Unavailable, Message: "GitHub Releases devolvió una respuesta no válida."); }
    }

    public static async Task DownloadAndApplyAsync(UpdateRelease release)
    {
        var target = Environment.ProcessPath ?? throw new InvalidOperationException("No se encontró ejecutable actual.");
        var directory = Path.GetDirectoryName(target) ?? throw new InvalidOperationException("No se encontró carpeta de instalación.");
        var download = Path.Combine(directory, $".AIUsage-{release.Tag}.new");

        using var hashResponse = await Client.GetAsync(release.Sha256Url);
        hashResponse.EnsureSuccessStatusCode();
        var expectedHash = (await hashResponse.Content.ReadAsStringAsync()).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (expectedHash is null || expectedHash.Length != 64 || !expectedHash.All(Uri.IsHexDigit))
            throw new InvalidOperationException("SHA-256 publicado no válido.");

        using (var downloadResponse = await Client.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            downloadResponse.EnsureSuccessStatusCode();
            await using var input = await downloadResponse.Content.ReadAsStreamAsync();
            await using var output = File.Create(download);
            await input.CopyToAsync(output);
        }

        string actualHash;
        await using (var downloadedFile = File.OpenRead(download))
            actualHash = Convert.ToHexString(await SHA256.HashDataAsync(downloadedFile));
        if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(download);
            throw new InvalidOperationException("La descarga no coincide con SHA-256 publicado.");
        }

        var updater = Path.Combine(Path.GetTempPath(), $"AIUsage-updater-{Guid.NewGuid():N}.exe");
        File.Copy(target, updater, overwrite: false);
        var info = new ProcessStartInfo(updater) { UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add("--apply-update");
        info.ArgumentList.Add(Environment.ProcessId.ToString());
        info.ArgumentList.Add(target);
        info.ArgumentList.Add(download);
        var process = Process.Start(info) ?? throw new InvalidOperationException("No se pudo iniciar actualizador.");
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AIUsage-Updater/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }
        [JsonPropertyName("assets")]
        public GitHubAsset[]? Assets { get; init; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }
        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; init; }
    }
}
