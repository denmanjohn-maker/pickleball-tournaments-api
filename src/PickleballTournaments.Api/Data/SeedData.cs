using Microsoft.EntityFrameworkCore;
using PickleballTournaments.Api.Domain;

namespace PickleballTournaments.Api.Data;

public class CitySeed
{
    public required string Name { get; set; }
    public required string State { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public required string Zip { get; set; }
}

public static class SeedData
{
    /// <summary>Upserts the configured metro cities (matched by Name+State) into the database.</summary>
    public static async Task SyncCitiesAsync(AppDbContext db, IConfiguration config, CancellationToken ct = default)
    {
        var seeds = config.GetSection("Cities").Get<List<CitySeed>>() ?? [];
        var existing = await db.MetroCities.ToListAsync(ct);

        foreach (var seed in seeds)
        {
            var city = existing.FirstOrDefault(c =>
                c.Name.Equals(seed.Name, StringComparison.OrdinalIgnoreCase) &&
                c.State.Equals(seed.State, StringComparison.OrdinalIgnoreCase));

            if (city is null)
            {
                db.MetroCities.Add(new MetroCity
                {
                    Name = seed.Name,
                    State = seed.State,
                    Latitude = seed.Latitude,
                    Longitude = seed.Longitude,
                    Zip = seed.Zip,
                    IsEnabled = true,
                });
            }
            else
            {
                city.Latitude = seed.Latitude;
                city.Longitude = seed.Longitude;
                city.Zip = seed.Zip;
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
