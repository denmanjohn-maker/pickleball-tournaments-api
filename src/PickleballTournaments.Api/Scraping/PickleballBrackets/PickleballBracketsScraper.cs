using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using PickleballTournaments.Api.Domain;
using PickleballTournaments.Api.Services;

namespace PickleballTournaments.Api.Scraping.PickleballBrackets;

public partial class PickleballBracketsScraper(
    IHttpClientFactory httpClientFactory,
    ServerActionDiscovery discovery,
    IOptions<ScrapingOptions> options,
    ILogger<PickleballBracketsScraper> logger) : ITournamentScraper
{
    public const string HttpClientName = "brackets";
    private const string BaseUrl = "https://pickleballtournaments.com";
    private const int PageSize = 25;

    public string SourceName => "pickleballtournaments.com";
    public TournamentSource Source => TournamentSource.PickleballBrackets;

    // "City, ST, USA" or "City, ST USA"
    [GeneratedRegex(@"^(?<city>.+?),\s*(?<state>[A-Z]{2})\b")]
    private static partial Regex LocationRegex();

    public async Task<IReadOnlyList<ScrapedTournament>> ScrapeAsync(
        MetroCity city, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var opts = options.Value;
        var results = new List<ScrapedTournament>();
        var page = 1;
        int totalCount;

        do
        {
            ct.ThrowIfCancellationRequested();
            var response = await InvokeGetTournamentsAsync(city, from, to, page, opts, ct);
            if (response?.Data is null)
                throw new InvalidOperationException(
                    $"getTournaments returned no data for {city.Name} page {page} (statusCode {response?.StatusCode}).");

            totalCount = response.Data.TotalCount;
            foreach (var item in response.Data.Items)
            {
                var mapped = Map(item, city, opts);
                if (mapped is not null && results.All(r => r.SourceId != mapped.SourceId))
                    results.Add(mapped);
            }

            page++;
            if (results.Count < totalCount && response.Data.Items.Count > 0)
                await Task.Delay(AllPickleballTournaments.AllPickleballTournamentsScraper.Jitter(opts), ct);
            else
                break;
        } while (page <= (totalCount + PageSize - 1) / PageSize);

        logger.LogInformation("Brackets: {Count} events near {City}, {State}", results.Count, city.Name, city.State);
        return results;
    }

    private async Task<GetTournamentsResponse?> InvokeGetTournamentsAsync(
        MetroCity city, DateOnly from, DateOnly to, int page, ScrapingOptions opts, CancellationToken ct)
    {
        var actionId = await discovery.GetActionIdAsync(ct);
        var response = await PostAsync(actionId, city, from, to, page, opts, ct);

        // A stale action ID (site redeployed) surfaces as a failed/empty flight response.
        if (response?.Data is null)
        {
            logger.LogWarning("getTournaments failed with cached action ID; re-discovering");
            actionId = await discovery.RefreshAsync(ct);
            response = await PostAsync(actionId, city, from, to, page, opts, ct);
        }

        return response;
    }

    private async Task<GetTournamentsResponse?> PostAsync(
        string actionId, MetroCity city, DateOnly from, DateOnly to, int page, ScrapingOptions opts, CancellationToken ct)
    {
        var box = GeoService.BoundingBoxFor(city.Latitude, city.Longitude, opts.RadiusMiles);
        var payload = new GetTournamentsRequest
        {
            PlaceKeyword = $"{city.Name}, {city.State}",
            Center = new LatLng(city.Latitude, city.Longitude),
            Bounds = new Bounds(box.NeLat, box.NeLng, box.SwLat, box.SwLng),
            DateFrom = from.ToString("yyyy-MM-dd"),
            DateTo = to.ToString("yyyy-MM-dd"),
            CurrentPage = page,
            UserLocation = new LatLng(city.Latitude, city.Longitude),
        };

        // Server actions take their arguments as a JSON array in a text/plain body.
        var body = JsonSerializer.Serialize(new[] { payload });
        var http = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/search")
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain"),
        };
        request.Headers.Add("Next-Action", actionId);

        using var httpResponse = await http.SendAsync(request, ct);
        httpResponse.EnsureSuccessStatusCode();
        var flight = await httpResponse.Content.ReadAsStringAsync(ct);
        return FlightResponseParser.Parse(flight);
    }

    private static ScrapedTournament? Map(TournamentItem item, MetroCity metro, ScrapingOptions opts)
    {
        var location = item.Location ?? "";
        var match = LocationRegex().Match(location);
        if (!match.Success)
            return null;

        double? lat = item.Lat != 0 ? item.Lat : null;
        double? lng = item.Lng != 0 ? item.Lng : null;

        // List items usually lack coordinates (server-side radius applies); when present, enforce it.
        if (lat is not null && lng is not null &&
            GeoService.DistanceMiles(metro.Latitude, metro.Longitude, lat.Value, lng.Value) > opts.RadiusMiles)
            return null;

        var url = item.Slug is { Length: > 0 } slug ? $"{BaseUrl}/tournaments/{slug}" : BaseUrl;
        return new ScrapedTournament
        {
            Name = item.Title,
            StartDate = DateOnly.FromDateTime(item.DateFrom),
            EndDate = item.DateTo is { } dt ? DateOnly.FromDateTime(dt) : null,
            City = match.Groups["city"].Value.Trim(),
            State = match.Groups["state"].Value,
            Latitude = lat,
            Longitude = lng,
            EntryFee = item.Price,
            Currency = item.Currency?.ToUpperInvariant(),
            RegistrationUrl = url,
            Source = TournamentSource.PickleballBrackets,
            SourceId = item.Id,
            SourceUrl = url,
            IsCanceled = item.IsCanceled,
        };
    }
}
