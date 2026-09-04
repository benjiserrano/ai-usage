namespace AIUsage;

public static class AppPlatform
{
    private static readonly Lazy<IPlatform> Instance = new(Detect);

    public static IPlatform Current => Instance.Value;

    private static IPlatform Detect()
    {
        if (OperatingSystem.IsWindows()) return new WindowsPlatform();
        if (OperatingSystem.IsMacOS()) return new MacPlatform();
        throw new PlatformNotSupportedException(
            $"AI Usage solo soporta Windows y macOS (detectado: {Environment.OSVersion.Platform}).");
    }
}
