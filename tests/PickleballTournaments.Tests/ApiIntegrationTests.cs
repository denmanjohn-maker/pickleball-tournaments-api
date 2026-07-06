using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PickleballTournaments.Api.Data;
using PickleballTournaments.Api.Domain;
using Xunit;

namespace PickleballTournaments.Tests;

public class ApiIntegrationTests : IClassFixture<ApiIntegrationTests.TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public ApiIntegrationTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetCities_ReturnsSeededMetros()
    {
        var client = _factory.CreateClient();
        var cities = await client.GetFromJsonAsync<List<CityResponse>>("/api/cities");

        Assert.NotNull(cities);
        Assert.Equal(25, cities!.Count);
        Assert.Contains(cities, c => c.Name == "Houston" && c.State == "TX");
    }

    [Fact]
    public async Task GetTournament_UnknownId_Returns404()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/tournaments/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTournaments_FiltersByCityAndWindow()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var houston = await db.MetroCities.FirstAsync(c => c.Name == "Houston");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var withinMonth = new Tournament
        {
            Name = "Integration Test Cup",
            NormalizedName = "integration test cup",
            StartDate = today.AddDays(10),
            City = "Houston",
            State = "TX",
            Source = TournamentSource.AllPickleballTournaments,
            SourceId = "test-1",
            SourceUrl = "https://example.com/1",
            DedupKey = "integration test cup|" + today.AddDays(10).ToString("yyyy-MM-dd") + "|TX",
            FirstScrapedAt = DateTime.UtcNow,
            LastScrapedAt = DateTime.UtcNow,
        };
        var outsideMonth = new Tournament
        {
            Name = "Integration Test Cup Later",
            NormalizedName = "integration test cup later",
            StartDate = today.AddDays(70),
            City = "Houston",
            State = "TX",
            Source = TournamentSource.AllPickleballTournaments,
            SourceId = "test-2",
            SourceUrl = "https://example.com/2",
            DedupKey = "integration test cup later|" + today.AddDays(70).ToString("yyyy-MM-dd") + "|TX",
            FirstScrapedAt = DateTime.UtcNow,
            LastScrapedAt = DateTime.UtcNow,
        };
        db.Tournaments.AddRange(withinMonth, outsideMonth);
        await db.SaveChangesAsync();
        db.TournamentCities.AddRange(
            new TournamentCity { TournamentId = withinMonth.Id, MetroCityId = houston.Id, DistanceMiles = 5 },
            new TournamentCity { TournamentId = outsideMonth.Id, MetroCityId = houston.Id, DistanceMiles = 5 });
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        var oneMonth = await client.GetFromJsonAsync<PagedResponse>("/api/tournaments?city=Houston&window=1m");
        var threeMonth = await client.GetFromJsonAsync<PagedResponse>("/api/tournaments?city=Houston&window=3m");

        Assert.NotNull(oneMonth);
        Assert.NotNull(threeMonth);
        Assert.Contains(oneMonth!.Items, t => t.Name == "Integration Test Cup");
        Assert.DoesNotContain(oneMonth.Items, t => t.Name == "Integration Test Cup Later");
        Assert.Contains(threeMonth!.Items, t => t.Name == "Integration Test Cup Later");
    }

    public class TestApiFactory : WebApplicationFactory<Program>
    {
        private SqliteConnection? _connection;

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Scraping:Enabled"] = "false",
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();

                _connection = new SqliteConnection("DataSource=:memory:");
                _connection.Open();

                services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _connection?.Dispose();
        }
    }

    private record CityResponse(int Id, string Name, string State, string Zip, int Events1Month, int Events3Month);
    private record TournamentResponse(int Id, string Name);
    private record PagedResponse(List<TournamentResponse> Items, int Page, int PageSize, int TotalCount);
}
