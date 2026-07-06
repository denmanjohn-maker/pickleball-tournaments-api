using Microsoft.Extensions.Options;
using PickleballTournaments.Api.Domain;

namespace PickleballTournaments.Api.Scraping.AllPickleballTournaments;

public class AllPickleballTournamentsScraper(
    IHttpClientFactory httpClientFactory,
    IOptions<ScrapingOptions> options,
    ILogger<AllPickleballTournamentsScraper> logger) : ITournamentScraper
{
    public const string HttpClientName = "apt";

    public string SourceName => "allpickleballtournaments.com";
    public TournamentSource Source => TournamentSource.AllPickleballTournaments;

    public async Task<IReadOnlyList<ScrapedTournament>> ScrapeAsync(
        MetroCity city, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var http = httpClientFactory.CreateClient(HttpClientName);
        var opts = options.Value;
        var results = new List<ScrapedTournament>();
        var seenPages = new HashSet<int>();
        var pageQueue = new Queue<int?>();
        pageQueue.Enqueue(null); // first request has no displaySegNum

        while (pageQueue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var page = pageQueue.Dequeue();

            var url = $"include/javaListEvents.asp?source=eventSearch" +
                      $"&startDate={from:yyyy-MM-dd}&endDate={to:yyyy-MM-dd}" +
                      $"&zip={city.Zip}&mileRadius={opts.RadiusMiles}";
            if (page is not null)
                url += $"&displaySegNum={page}";

            var response = await http.GetStringAsync(url, ct);
            var parsed = JavaListEventsParser.Parse(response, from, to);

            foreach (var t in parsed.Tournaments)
            {
                if (results.All(r => r.SourceId != t.SourceId))
                    results.Add(t);
            }

            foreach (var next in parsed.AdditionalPages)
            {
                if (seenPages.Add(next))
                    pageQueue.Enqueue(next);
            }

            if (pageQueue.Count > 0)
                await Task.Delay(Jitter(opts), ct);
        }

        logger.LogInformation("APT: {Count} events near {City}, {State}", results.Count, city.Name, city.State);
        return results;
    }

    internal static TimeSpan Jitter(ScrapingOptions opts) =>
        TimeSpan.FromMilliseconds(Random.Shared.Next(opts.MinDelayMs, opts.MaxDelayMs + 1));
}
