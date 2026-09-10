using System.Text.Json;
using Xunit;

namespace AIUsage.Tests;

public sealed class UiScaleTests
{
    [Fact]
    public void Width_and_scale_are_inverse_of_each_other()
    {
        Assert.Equal(UiScale.DesignWidth, UiScale.WidthFor(1.0));
        Assert.Equal(1.5, UiScale.ScaleFor(UiScale.WidthFor(1.5)), 6);
    }

    [Theory]
    [InlineData(0.1, UiScale.Min)]
    [InlineData(99, UiScale.Max)]
    public void Scale_stays_inside_the_supported_range(double requested, double expected) =>
        Assert.Equal(expected, UiScale.Clamp(requested));

    [Theory]
    [InlineData(0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Corrupt_scales_fall_back_to_the_default(double stored) =>
        Assert.Equal(UiScale.DefaultCompact, UiScale.Normalize(stored, UiScale.DefaultCompact));

    [Fact]
    public void Legacy_settings_get_the_default_scales()
    {
        var settings = JsonSerializer.Deserialize<WindowSettings>("{\"Left\":10,\"Top\":20}");
        Assert.NotNull(settings);
        Assert.Equal(UiScale.DefaultFull, UiScale.Normalize(settings.FullScale, UiScale.DefaultFull));
        Assert.Equal(UiScale.DefaultCompact, UiScale.Normalize(settings.CompactScale, UiScale.DefaultCompact));
    }

    [Fact]
    public void Saved_scales_survive_a_round_trip()
    {
        var json = JsonSerializer.Serialize(new WindowSettings(1, 2, true, 1.4, 2.2));
        var settings = JsonSerializer.Deserialize<WindowSettings>(json);
        Assert.NotNull(settings);
        Assert.Equal(1.4, settings.FullScale);
        Assert.Equal(2.2, settings.CompactScale);
    }
}
