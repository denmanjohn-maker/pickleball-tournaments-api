namespace PickleballTournaments.Api.Domain;

public enum TournamentSource
{
    PickleballBrackets = 1,
    AllPickleballTournaments = 2,
}

public class Tournament
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? VenueName { get; set; }
    public required string City { get; set; }
    public required string State { get; set; }
    public string? Zip { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public decimal? EntryFee { get; set; }
    public string? Currency { get; set; }
    public string? RegistrationUrl { get; set; }
    public TournamentSource Source { get; set; }
    public required string SourceId { get; set; }
    public required string SourceUrl { get; set; }
    public bool IsCanceled { get; set; }
    public DateTime FirstScrapedAt { get; set; }
    public DateTime LastScrapedAt { get; set; }
    public required string DedupKey { get; set; }

    /// <summary>When set, this row is a cross-source duplicate of the referenced canonical row.</summary>
    public int? CanonicalTournamentId { get; set; }
    public Tournament? CanonicalTournament { get; set; }

    public List<TournamentCity> Cities { get; set; } = [];
}
