using PickleballTournaments.Api.Domain;

namespace PickleballTournaments.Api.Scraping;

/// <summary>Source-agnostic result of scraping one tournament listing.</summary>
public record ScrapedTournament
{
    public required string Name { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public string? VenueName { get; init; }
    public required string City { get; init; }
    public required string State { get; init; }
    public string? Zip { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public decimal? EntryFee { get; init; }
    public string? Currency { get; init; }
    public string? RegistrationUrl { get; init; }
    public TournamentSource Source { get; init; }
    public required string SourceId { get; init; }
    public required string SourceUrl { get; init; }
    public bool IsCanceled { get; init; }
}
