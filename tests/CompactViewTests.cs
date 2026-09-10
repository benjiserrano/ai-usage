using System;
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
    public void Legacy_settings_keep_disconnected_providers_visible()
    {
        var settings = JsonSerializer.Deserialize<WindowSettings>("{\"Left\":10,\"Top\":20}");
        Assert.NotNull(settings);
        Assert.True(settings.ShowDisconnectedProviders);
        Assert.Null(settings.CompactLeft);
        Assert.Null(settings.CompactTop);
    }

    [Fact]
    public void Provider_filter_hides_non_available_snapshots()
    {
        using var coordinator = new UsageCoordinator();
        coordinator.Snapshots.Add(new UsageSnapshot("Connected", ProviderState.Available, [], DateTimeOffset.UtcNow));
        coordinator.Snapshots.Add(new UsageSnapshot("Disconnected", ProviderState.AuthRequired, [], DateTimeOffset.UtcNow));

        coordinator.ShowDisconnectedProviders = false;

        Assert.Single(coordinator.VisibleSnapshots);
        Assert.Equal("Connected", coordinator.VisibleSnapshots.Single().Provider);
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
