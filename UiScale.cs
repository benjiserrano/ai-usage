namespace AIUsage;

/// <summary>
/// Todo el contenido se dibuja sobre un lienzo de <see cref="DesignWidth"/> puntos y se
/// amplía con un único factor, así la tipografía y los márgenes crecen a la vez y la
/// relación de aspecto no cambia: el ancho manda y el alto lo deduce el propio contenido.
/// </summary>
public static class UiScale
{
    public const double DesignWidth = 270;
    public const double DesignMinHeight = 150;
    public const double Min = 0.85;
    public const double Max = 3.0;
    public const double Step = 0.1;
    /// <summary>Puntos de ancho que hay que mover para tomarlo por un arrastre y no por el redondeo del gestor de ventanas.</summary>
    public const double Deadband = 4.0;
    public const double DefaultFull = 1.0;
    public const double DefaultCompact = 1.35;

    public static double Clamp(double scale) => Math.Clamp(scale, Min, Max);

    /// <summary>Descarta los valores corruptos de settings.json y cae al valor por defecto.</summary>
    public static double Normalize(double scale, double fallback) =>
        double.IsFinite(scale) && scale > 0 ? Clamp(scale) : Clamp(fallback);

    public static double WidthFor(double scale) => DesignWidth * Clamp(scale);

    public static double ScaleFor(double width) =>
        double.IsFinite(width) && width > 0 ? Clamp(width / DesignWidth) : Min;
}
