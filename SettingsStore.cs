using System.Text.Json; using System.IO;
namespace AIUsage;
public sealed record WindowSettings(double Left, double Top, bool CompactMode = false);
public static class SettingsStore
{
    private static readonly string Dir = AppPlatform.Current.SettingsDirectory;
    private static readonly string FileName = Path.Combine(Dir, "settings.json");
    public static WindowSettings Load() { try { return JsonSerializer.Deserialize<WindowSettings>(File.ReadAllText(FileName)) ?? new(0, 0); } catch { return new(0, 0); } }
    public static void Save(WindowSettings value) { Directory.CreateDirectory(Dir); var tmp = FileName + ".tmp"; File.WriteAllText(tmp, JsonSerializer.Serialize(value)); File.Move(tmp, FileName, true); }
}
