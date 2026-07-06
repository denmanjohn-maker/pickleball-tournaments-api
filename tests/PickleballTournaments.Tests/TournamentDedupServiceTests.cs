using PickleballTournaments.Api.Services;
using Xunit;

namespace PickleballTournaments.Tests;

public class TournamentDedupServiceTests
{
    [Theory]
    [InlineData("CASA Heatwave!", "casa heatwave")]
    [InlineData("World of Pickleball - Houston - $2,000", "world of pickleball houston 2 000")]
    [InlineData("  Extra   Spaces  ", "extra spaces")]
    public void Normalize_StripsPunctuationAndCase(string input, string expected)
    {
        Assert.Equal(expected, TournamentDedupService.Normalize(input));
    }

    [Fact]
    public void BuildDedupKey_SameInputs_ProduceSameKey()
    {
        var key1 = TournamentDedupService.BuildDedupKey("casa heatwave", new DateOnly(2026, 7, 24), "tx");
        var key2 = TournamentDedupService.BuildDedupKey("casa heatwave", new DateOnly(2026, 7, 24), "TX");
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void BuildDedupKey_DifferentDates_ProduceDifferentKeys()
    {
        var key1 = TournamentDedupService.BuildDedupKey("casa heatwave", new DateOnly(2026, 7, 24), "TX");
        var key2 = TournamentDedupService.BuildDedupKey("casa heatwave", new DateOnly(2026, 7, 25), "TX");
        Assert.NotEqual(key1, key2);
    }
}
