using PickleballTournaments.Api.Domain;

namespace PickleballTournaments.Api.Scraping;

public interface ITournamentScraper
{
    string SourceName { get; }
    TournamentSource Source { get; }

    Task<IReadOnlyList<ScrapedTournament>> ScrapeAsync(
        MetroCity city, DateOnly from, DateOnly to, CancellationToken ct);
}
