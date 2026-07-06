using PickleballTournaments.Api.Scraping.PickleballBrackets;
using Xunit;

namespace PickleballTournaments.Tests;

public class FlightResponseParserTests
{
    private static string LoadFixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void Parse_LiveHoustonFixture_ExtractsItemsAndTotalCount()
    {
        var response = LoadFixture("brackets-flight-page1.txt");
        var parsed = FlightResponseParser.Parse(response);

        Assert.NotNull(parsed);
        Assert.NotNull(parsed!.Data);
        Assert.Equal(48, parsed.Data!.TotalCount);
        Assert.Equal(25, parsed.Data.Items.Count);

        var first = parsed.Data.Items[0];
        Assert.Equal("Liveball x Courtside Moneyball", first.Title);
        Assert.Equal("liveball-x-courtside-moneyball", first.Slug);
        Assert.Equal("Tomball, TX, USA", first.Location);
    }

    [Fact]
    public void Parse_ErrorResponse_ReturnsNullData()
    {
        var response = LoadFixture("brackets-flight-error.txt");
        var parsed = FlightResponseParser.Parse(response);

        Assert.NotNull(parsed);
        Assert.Null(parsed!.Data);
        Assert.Equal(400, parsed.StatusCode);
    }

    [Fact]
    public void Parse_MissingDataLine_ReturnsNull()
    {
        var parsed = FlightResponseParser.Parse("0:{\"a\":\"$@1\"}\n");
        Assert.Null(parsed);
    }
}
