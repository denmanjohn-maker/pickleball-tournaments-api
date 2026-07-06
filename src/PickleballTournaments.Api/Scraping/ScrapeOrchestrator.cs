using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PickleballTournaments.Api.Data;
using PickleballTournaments.Api.Domain;
using PickleballTournaments.Api.Services;

namespace PickleballTournaments.Api.Scraping;

public class ScrapeOrchestrator(
    IServiceScopeFactory scopeFactory,
    AppDbContext db,
    IEnumerable<ITournamentScraper> scrapers,
    IOptions<ScrapingOptions> options,
    ILogger<ScrapeOrchestrator> logger)
{
    // SQLite allows only one writer at a time; scrapers run concurrently but must serialize writes,
    // each through its own AppDbContext instance (DbContext is not thread-safe).
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    public async Task<ScrapeRun> RunAsync(ScrapeTrigger trigger, CancellationToken ct)
    {
        var run = new ScrapeRun
        {
            StartedUtc = DateTime.UtcNow,
            Trigger = trigger,
            Status = ScrapeStatus.Running,
        };
        db.ScrapeRuns.Add(run);
        await db.SaveChangesAsync(ct);

        var opts = options.Value;
        var from = DateOnly.FromDateTime(DateTime.UtcNow);
        var to = from.AddMonths(opts.WindowMonths);
        var cities = await db.MetroCities.Where(c => c.IsEnabled).ToListAsync(ct);

        var errors = new List<string>();
        var scraperList = scrapers.ToList();

        await Task.WhenAll(scraperList.Select(scraper =>
            RunScraperAsync(scraper, cities, from, to, run, errors, ct)));

        run.CompletedUtc = DateTime.UtcNow;
        run.ErrorCount = errors.Count;
        run.ErrorSummary = errors.Count == 0 ? null : string.Join("; ", errors.Take(10));
        run.Status = errors.Count == 0
            ? ScrapeStatus.Succeeded
            : errors.Count >= scraperList.Count * cities.Count
                ? ScrapeStatus.Failed
                : ScrapeStatus.PartialFailure;

        await db.SaveChangesAsync(ct);
        return run;
    }

    private async Task RunScraperAsync(
        ITournamentScraper scraper, List<MetroCity> cities, DateOnly from, DateOnly to,
        ScrapeRun run, List<string> errors, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var city in cities)
        {
            try
            {
                var found = await scraper.ScrapeAsync(city, from, to, ct);
                lock (run)
                {
                    if (scraper.Source == TournamentSource.PickleballBrackets)
                        run.BracketsFound += found.Count;
                    else
                        run.AptFound += found.Count;
                }

                await WriteLock.WaitAsync(ct);
                try
                {
                    foreach (var scraped in found)
                        await UpsertAsync(scopedDb, scraped, city, run, ct);

                    await scopedDb.SaveChangesAsync(ct);
                }
                finally
                {
                    WriteLock.Release();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scrape failed for {Source} / {City}", scraper.SourceName, city.Name);
                lock (errors)
                    errors.Add($"{scraper.SourceName}/{city.Name}: {ex.Message}");
            }
        }
    }

    private static async Task UpsertAsync(
        AppDbContext scopedDb, ScrapedTournament scraped, MetroCity city, ScrapeRun run, CancellationToken ct)
    {
        var normalizedName = TournamentDedupService.Normalize(scraped.Name);
        var dedupKey = TournamentDedupService.BuildDedupKey(normalizedName, scraped.StartDate, scraped.State);
        var now = DateTime.UtcNow;

        var existing = await scopedDb.Tournaments
            .FirstOrDefaultAsync(t => t.Source == scraped.Source && t.SourceId == scraped.SourceId, ct);

        Tournament tournament;
        if (existing is not null)
        {
            existing.Name = scraped.Name;
            existing.NormalizedName = normalizedName;
            existing.StartDate = scraped.StartDate;
            existing.EndDate = scraped.EndDate;
            existing.VenueName = scraped.VenueName;
            existing.City = scraped.City;
            existing.State = scraped.State;
            existing.Zip = scraped.Zip;
            existing.Latitude = scraped.Latitude;
            existing.Longitude = scraped.Longitude;
            existing.EntryFee = scraped.EntryFee;
            existing.Currency = scraped.Currency;
            existing.RegistrationUrl = scraped.RegistrationUrl;
            existing.SourceUrl = scraped.SourceUrl;
            existing.IsCanceled = scraped.IsCanceled;
            existing.LastScrapedAt = now;
            existing.DedupKey = dedupKey;
            tournament = existing;
            lock (run) run.Updated++;
        }
        else
        {
            tournament = new Tournament
            {
                Name = scraped.Name,
                NormalizedName = normalizedName,
                StartDate = scraped.StartDate,
                EndDate = scraped.EndDate,
                VenueName = scraped.VenueName,
                City = scraped.City,
                State = scraped.State,
                Zip = scraped.Zip,
                Latitude = scraped.Latitude,
                Longitude = scraped.Longitude,
                EntryFee = scraped.EntryFee,
                Currency = scraped.Currency,
                RegistrationUrl = scraped.RegistrationUrl,
                Source = scraped.Source,
                SourceId = scraped.SourceId,
                SourceUrl = scraped.SourceUrl,
                IsCanceled = scraped.IsCanceled,
                FirstScrapedAt = now,
                LastScrapedAt = now,
                DedupKey = dedupKey,
            };
            scopedDb.Tournaments.Add(tournament);
            lock (run) run.Inserted++;

            // Link to an existing row from the other source with the same dedup key.
            var canonical = await scopedDb.Tournaments
                .Where(t => t.DedupKey == dedupKey && t.Source != scraped.Source && t.CanonicalTournamentId == null)
                .FirstOrDefaultAsync(ct);
            if (canonical is not null)
                tournament.CanonicalTournamentId = canonical.Id;
        }

        var link = await scopedDb.TournamentCities
            .FirstOrDefaultAsync(tc => tc.MetroCityId == city.Id &&
                (tc.Tournament.Source == scraped.Source && tc.Tournament.SourceId == scraped.SourceId), ct);

        double? distance = scraped.Latitude is { } lat && scraped.Longitude is { } lng
            ? GeoService.DistanceMiles(city.Latitude, city.Longitude, lat, lng)
            : null;

        if (link is null)
        {
            scopedDb.TournamentCities.Add(new TournamentCity
            {
                Tournament = tournament,
                MetroCityId = city.Id,
                DistanceMiles = distance,
            });
        }
        else
        {
            link.DistanceMiles = distance;
        }
    }
}
