using System.Diagnostics;

namespace AIUsage;

public static class UpdateBootstrapper
{
    public static bool TryApply(string[] args)
    {
        if (args.Length != 4 || args[0] != "--apply-update" || !int.TryParse(args[1], out var processId)) return false;

        try
        {
            using var process = Process.GetProcessById(processId);
            process.WaitForExit(30_000);
        }
        catch (ArgumentException) { }

        try
        {
            var target = args[2];
            var download = args[3];
            if (!File.Exists(download)) return true;
            File.Move(download, target, overwrite: true);
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch { }
        finally { DeleteSelfLater(Environment.ProcessPath); }
        return true;
    }

    private static void DeleteSelfLater(string? updater)
    {
        if (string.IsNullOrWhiteSpace(updater)) return;
        try
        {
            var info = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true };
            info.ArgumentList.Add("/c");
            info.ArgumentList.Add($"timeout /t 2 /nobreak > nul & del /f /q \"{updater}\"");
            Process.Start(info);
        }
        catch { }
    }
}
