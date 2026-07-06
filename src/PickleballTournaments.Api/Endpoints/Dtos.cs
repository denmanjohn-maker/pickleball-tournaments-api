using PickleballTournaments.Api.Domain;

namespace PickleballTournaments.Api.Endpoints;

public record TournamentDto(
    int Id,
    string Name,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? VenueName,
    string City,
    string State,
    decimal? EntryFee,
    string? Currency,
    string? RegistrationUrl,
    string Source,
    string SourceUrl,
    bool IsCanceled)
{
    public static TournamentDto From(Tournament t) => new(
        t.Id, t.Name, t.StartDate, t.EndDate, t.VenueName, t.City, t.State,
        t.EntryFee, t.Currency, t.RegistrationUrl, t.Source.ToString(), t.SourceUrl, t.IsCanceled);
}

public record TournamentDetailDto(
    int Id,
    string Name,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? VenueName,
    string City,
    string State,
    decimal? EntryFee,
    string? Currency,
    string? RegistrationUrl,
    string Source,
    string SourceUrl,
    bool IsCanceled,
    IReadOnlyList<TournamentDto> RelatedSourceListings);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public record CityDto(int Id, string Name, string State, string Zip, int Events1Month, int Events3Month);

public record ScrapeRunDto(
    int Id,
    DateTime StartedUtc,
    DateTime? CompletedUtc,
    string Trigger,
    string Status,
    int BracketsFound,
    int AptFound,
    int Inserted,
    int Updated,
    int ErrorCount,
    string? ErrorSummary)
{
    public static ScrapeRunDto From(ScrapeRun r) => new(
        r.Id, r.StartedUtc, r.CompletedUtc, r.Trigger.ToString(), r.Status.ToString(),
        r.BracketsFound, r.AptFound, r.Inserted, r.Updated, r.ErrorCount, r.ErrorSummary);
}
