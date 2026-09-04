using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace AIUsage.Tests;

public sealed class CompactViewTests
{
    [Fact]
    public void Legacy_settings_keep_compact_mode_disabled()
    {
        var settings = JsonSerializer.Deserialize<WindowSettings>("{\"Left\":10,\"Top\":20}");
        Assert.NotNull(settings);
        Assert.False(settings.CompactMode);
    }

    [Fact]
    public void Compact_view_limits_each_provider_to_two_windows()
    {
        var windows = new[]
        {
            new QuotaWindow("one", "1", 90, null),
            new QuotaWindow("two", "2", 80, null),
            new QuotaWindow("three", "3", 70, null)
        };
        var converter = new CompactWindowsConverter();

        var result = Assert.IsAssignableFrom<IEnumerable<QuotaWindow>>(
            converter.Convert(windows, typeof(object), null!, CultureInfo.InvariantCulture));

        Assert.Equal(["one", "two"], result.Select(window => window.Id));
    }

    [Fact]
    public void Reset_time_is_empty_when_unknown()
    {
        var converter = new ResetTimeConverter();
        Assert.Equal("", converter.Convert(null!, typeof(string), null!, CultureInfo.InvariantCulture));
    }
}
