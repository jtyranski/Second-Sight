using SecondSight.Core;

namespace SecondSight.Tests;

public class AvatarNameTests
{
    [Theory]
    [InlineData("Jimmi Tiponi", "Jimmi", "Tiponi")]
    [InlineData("jimmi.tiponi", "jimmi", "tiponi")]
    [InlineData("  DexterFrost  ", "DexterFrost", "Resident")]
    [InlineData("DexterFrost Resident", "DexterFrost", "Resident")]
    public void Parse_splits_first_and_last(string input, string first, string last)
    {
        var name = AvatarName.Parse(input);
        Assert.Equal(first, name.First);
        Assert.Equal(last, name.Last);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("too many name parts")]
    public void Parse_rejects_bad_names(string input) =>
        Assert.Throws<ArgumentException>(() => AvatarName.Parse(input));

    [Fact]
    public void DisplayName_hides_Resident()
    {
        Assert.Equal("DexterFrost", AvatarName.Parse("DexterFrost").DisplayName);
        Assert.Equal("Jimmi Tiponi", AvatarName.Parse("Jimmi Tiponi").DisplayName);
    }

    [Theory]
    [InlineData("Jimmi Tiponi", "jimmi tiponi", true)]
    [InlineData("Jimmi Tiponi", "jimmi.tiponi", true)]
    [InlineData("Jimmi Tiponi", "Jimmi Tiponix", false)]
    [InlineData("DexterFrost", "DexterFrost Resident", true)]
    [InlineData("DexterFrost", "", false)]
    [InlineData("Jimmi Tiponi", "a b c", false)]
    public void Matches_ignores_case_and_Resident(string name, string other, bool expected) =>
        Assert.Equal(expected, AvatarName.Parse(name).Matches(other));
}
