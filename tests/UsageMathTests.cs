using System; using Xunit;
namespace AIUsage.Tests;
public sealed class UsageMathTests
{
    [Theory]
    [InlineData(0, 100)] [InlineData(23, 77)] [InlineData(100, 0)] [InlineData(150, 0)] [InlineData(-5, 100)]
    public void Remaining_is_clamped(double used, double expected) => Assert.Equal(expected, UsageMath.Remaining(used));
    [Fact] public void Duration_labels_match_compact_panel() { Assert.Equal("5h", UsageMath.Label(TimeSpan.FromMinutes(300))); Assert.Equal("7d", UsageMath.Label(TimeSpan.FromDays(7))); }
}
