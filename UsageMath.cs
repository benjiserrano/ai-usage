namespace AIUsage;
public static class UsageMath
{
    public static double Remaining(double usedPercent) => Math.Clamp(100d - usedPercent, 0d, 100d);
    public static string Label(TimeSpan duration) => duration.TotalDays >= 1 ? $"{Math.Round(duration.TotalDays)}d" : $"{Math.Round(duration.TotalHours)}h";
    public static string ResetText(DateTimeOffset? reset) => reset is null ? "reset desconocido" : $"reset {reset.Value.ToLocalTime():dd/MM HH:mm}";
}
