using Microsoft.EntityFrameworkCore;
using PickleballTournaments.Api.Data;

namespace PickleballTournaments.Api.Endpoints;

public static class CityEndpoints
{
    public static void MapCityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/cities", async (AppDbContext db) =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var oneMonth = today.AddMonths(1);
            var threeMonths = today.AddMonths(3);

            var cities = await db.MetroCities.AsNoTracking()
                .Where(c => c.IsEnabled)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var counts = await db.TournamentCities.AsNoTracking()
                .Where(tc => tc.Tournament.CanonicalTournamentId == null && !tc.Tournament.IsCanceled)
                .Select(tc => new { tc.MetroCityId, tc.Tournament.StartDate })
                .ToListAsync();

            var result = cities.Select(c =>
            {
                var cityCounts = counts.Where(x => x.MetroCityId == c.Id).ToList();
                return new CityDto(
                    c.Id, c.Name, c.State, c.Zip,
                    cityCounts.Count(x => x.StartDate >= today && x.StartDate <= oneMonth),
                    cityCounts.Count(x => x.StartDate >= today && x.StartDate <= threeMonths));
            }).ToList();

            return Results.Ok(result);
        }).WithTags("Cities");
    }
}
