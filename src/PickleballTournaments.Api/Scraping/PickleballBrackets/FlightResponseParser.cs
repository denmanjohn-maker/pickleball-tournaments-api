using System.Text.Json;

namespace PickleballTournaments.Api.Scraping.PickleballBrackets;

/// <summary>
/// Parses the React "flight" wire format returned by Next.js server actions:
/// newline-delimited "N:{json}" rows. The row with id 1 carries the action's return value.
/// </summary>
public static class FlightResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static GetTournamentsResponse? Parse(string flightResponse)
    {
        foreach (var line in flightResponse.Split('\n'))
        {
            if (!line.StartsWith("1:", StringComparison.Ordinal))
                continue;

            var json = line[2..].Trim();
            if (json.Length == 0 || json[0] != '{')
                return null;

            return JsonSerializer.Deserialize<GetTournamentsResponse>(json, JsonOptions);
        }
        return null;
    }
}
