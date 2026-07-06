using System.Text.RegularExpressions;

namespace PickleballTournaments.Api.Scraping.PickleballBrackets;

/// <summary>
/// Discovers the Next.js server-action ID for getTournaments at runtime. The ID rotates on
/// every site deployment, so it is cached and re-discovered when requests start failing.
/// </summary>
public partial class ServerActionDiscovery(
    IHttpClientFactory httpClientFactory,
    ILogger<ServerActionDiscovery> logger)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _cachedActionId;

    [GeneratedRegex(@"/_next/static/chunks/[^""']+\.js")]
    private static partial Regex ChunkUrlRegex();

    [GeneratedRegex(@"createServerReference\)?\(""(?<id>[0-9a-f]{40,})""[^)]{0,120}""getTournaments""\)")]
    private static partial Regex ActionIdRegex();

    public async Task<string> GetActionIdAsync(CancellationToken ct)
    {
        if (_cachedActionId is not null)
            return _cachedActionId;
        return await DiscoverAsync(ct);
    }

    /// <summary>Drops the cache and re-discovers; call after a request fails with the cached ID.</summary>
    public async Task<string> RefreshAsync(CancellationToken ct)
    {
        _cachedActionId = null;
        return await DiscoverAsync(ct);
    }

    private async Task<string> DiscoverAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_cachedActionId is not null)
                return _cachedActionId;

            var http = httpClientFactory.CreateClient(PickleballBracketsScraper.HttpClientName);
            var searchHtml = await http.GetStringAsync("/search", ct);

            var chunkUrls = ChunkUrlRegex().Matches(searchHtml)
                .Select(m => m.Value)
                .Distinct()
                .ToList();

            foreach (var chunkUrl in chunkUrls)
            {
                ct.ThrowIfCancellationRequested();
                string js;
                try
                {
                    js = await http.GetStringAsync(chunkUrl, ct);
                }
                catch (HttpRequestException ex)
                {
                    logger.LogWarning(ex, "Failed to fetch chunk {Url}", chunkUrl);
                    continue;
                }

                var id = ExtractActionId(js);
                if (id is not null)
                {
                    logger.LogInformation("Discovered getTournaments action ID {Id} in {Url}", id, chunkUrl);
                    _cachedActionId = id;
                    return id;
                }
            }

            throw new InvalidOperationException(
                $"getTournaments server-action ID not found in any of {chunkUrls.Count} script chunks on /search.");
        }
        finally
        {
            _lock.Release();
        }
    }

    internal static string? ExtractActionId(string chunkJs)
    {
        var match = ActionIdRegex().Match(chunkJs);
        return match.Success ? match.Groups["id"].Value : null;
    }
}
