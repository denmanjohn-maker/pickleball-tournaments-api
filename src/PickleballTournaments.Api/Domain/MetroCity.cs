namespace PickleballTournaments.Api.Domain;

public class MetroCity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string State { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public required string Zip { get; set; }
    public bool IsEnabled { get; set; } = true;

    public List<TournamentCity> Tournaments { get; set; } = [];
}

public class TournamentCity
{
    public int TournamentId { get; set; }
    public Tournament Tournament { get; set; } = null!;
    public int MetroCityId { get; set; }
    public MetroCity MetroCity { get; set; } = null!;

    /// <summary>Null when the source lacks coordinates; membership then comes from the source's own radius search.</summary>
    public double? DistanceMiles { get; set; }
}
