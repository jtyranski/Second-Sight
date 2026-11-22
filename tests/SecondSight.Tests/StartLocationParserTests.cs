using SecondSight.Core.Session;

namespace SecondSight.Tests;

public class StartLocationParserTests
{
    [Theory]
    [InlineData(null, "last")]
    [InlineData("", "last")]
    [InlineData("LAST", "last")]
    [InlineData("home", "home")]
    [InlineData("Ahern", "uri:Ahern&128&128&30")]
    [InlineData("Da Boom/10/20/30", "uri:Da Boom&10&20&30")]
    [InlineData("Ahern/50", "uri:Ahern&50&128&30")]
    public void Parses_valid_locations(string? input, string expected)
    {
        Assert.True(StartLocationParser.TryParse(input, out var start, out var error));
        Assert.Null(error);
        Assert.Equal(expected, start);
    }

    [Theory]
    [InlineData("/1/2/3")]
    [InlineData("Ahern/x/2/3")]
    [InlineData("Ahern/1/2/3/4")]
    public void Rejects_invalid_locations(string input)
    {
        Assert.False(StartLocationParser.TryParse(input, out _, out var error));
        Assert.NotNull(error);
    }
}
