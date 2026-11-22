using System.Text;
using LibreMetaverse;
using SecondSight.Core.Travel;

namespace SecondSight.Tests;

public class LureLocationTests
{
    private static byte[] Bucket(string s) => Encoding.UTF8.GetBytes(s + "\0");

    [Fact]
    public void Parses_viewer_lure_bucket()
    {
        Assert.True(LureLocation.TryParse(Bucket("256000|256512|128.5|64|25|0|1|0|M"), out var loc));
        Assert.Equal(((ulong)256000 << 32) | 256512, loc!.RegionHandle);
        Assert.Equal(new Vector3(128.5f, 64, 25), loc.Position);
    }

    [Fact]
    public void Rounds_global_position_down_to_region_corner()
    {
        Assert.True(LureLocation.TryParse(Bucket("256100|256700|10|20|30"), out var loc));
        Assert.Equal(((ulong)256000 << 32) | 256512, loc!.RegionHandle);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("1|2|3")]
    [InlineData("a|b|1|2|3")]
    public void Rejects_unreadable_buckets(string text) =>
        Assert.False(LureLocation.TryParse(Bucket(text), out _));

    [Fact]
    public void Rejects_null() => Assert.False(LureLocation.TryParse(null, out _));
}
