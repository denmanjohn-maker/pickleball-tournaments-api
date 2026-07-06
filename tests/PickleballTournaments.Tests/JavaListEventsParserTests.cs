using PickleballTournaments.Api.Scraping.AllPickleballTournaments;
using Xunit;

namespace PickleballTournaments.Tests;

public class JavaListEventsParserTests
{
    private static string LoadFixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void Parse_HoustonFixture_ReturnsFiveTournaments()
    {
        var response = LoadFixture("apt-houston-results.js");
        var result = JavaListEventsParser.Parse(response, new DateOnly(2026, 7, 6), new DateOnly(2026, 10, 6));

        Assert.Equal(5, result.Tournaments.Count);
        Assert.Empty(result.AdditionalPages);

        var heatwave = Assert.Single(result.Tournaments, t => t.SourceId == "52296");
        Assert.Equal("CASA Heatwave", heatwave.Name);
        Assert.Equal(new DateOnly(2026, 7, 24), heatwave.StartDate);
        Assert.Equal(new DateOnly(2026, 7, 26), heatwave.EndDate);
        Assert.Equal("Houston", heatwave.City);
        Assert.Equal("TX", heatwave.State);
        Assert.Equal("https://www.allpickleballtournaments.com/event-details.asp?TID=52296", heatwave.SourceUrl);
    }

    [Fact]
    public void Parse_EmptyResponse_ReturnsNoTournaments()
    {
        var result = JavaListEventsParser.Parse("", new DateOnly(2026, 7, 6), new DateOnly(2026, 10, 6));
        Assert.Empty(result.Tournaments);
        Assert.Empty(result.AdditionalPages);
    }

    [Theory]
    [InlineData("Jul<br>10-12", 2026, 7, 6, 2026, 10, 6, 2026, 7, 10, 2026, 7, 12)]
    [InlineData("Dec<br>30-2", 2026, 12, 1, 2027, 3, 1, 2026, 12, 30, 2027, 1, 2)]
    public void ParseDateCell_ResolvesYearFromWindow(
        string cell,
        int wfy, int wfm, int wfd, int wty, int wtm, int wtd,
        int sy, int sm, int sd, int ey, int em, int ed)
    {
        var from = new DateOnly(wfy, wfm, wfd);
        var to = new DateOnly(wty, wtm, wtd);
        var (start, end) = JavaListEventsParser.ParseDateCell(cell, from, to);

        Assert.Equal(new DateOnly(sy, sm, sd), start);
        Assert.Equal(new DateOnly(ey, em, ed), end);
    }
}
