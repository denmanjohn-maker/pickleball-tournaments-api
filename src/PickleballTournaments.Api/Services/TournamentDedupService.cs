using System.Text.RegularExpressions;

namespace PickleballTournaments.Api.Services;

public static partial class TournamentDedupService
{
    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlphaNumRegex();

    public static string Normalize(string name) =>
        NonAlphaNumRegex().Replace(name.ToLowerInvariant(), " ").Trim();

    public static string BuildDedupKey(string normalizedName, DateOnly startDate, string state) =>
        $"{normalizedName}|{startDate:yyyy-MM-dd}|{state.ToUpperInvariant()}";
}
