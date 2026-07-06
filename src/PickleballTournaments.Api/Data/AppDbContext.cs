using Microsoft.EntityFrameworkCore;
using PickleballTournaments.Api.Domain;

namespace PickleballTournaments.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<MetroCity> MetroCities => Set<MetroCity>();
    public DbSet<TournamentCity> TournamentCities => Set<TournamentCity>();
    public DbSet<ScrapeRun> ScrapeRuns => Set<ScrapeRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tournament>(e =>
        {
            e.HasIndex(t => new { t.Source, t.SourceId }).IsUnique();
            e.HasIndex(t => t.DedupKey);
            e.HasIndex(t => new { t.State, t.StartDate });
            e.HasOne(t => t.CanonicalTournament)
                .WithMany()
                .HasForeignKey(t => t.CanonicalTournamentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TournamentCity>(e =>
        {
            e.HasKey(tc => new { tc.TournamentId, tc.MetroCityId });
            e.HasOne(tc => tc.Tournament).WithMany(t => t.Cities).HasForeignKey(tc => tc.TournamentId);
            e.HasOne(tc => tc.MetroCity).WithMany(c => c.Tournaments).HasForeignKey(tc => tc.MetroCityId);
        });

        modelBuilder.Entity<MetroCity>(e =>
        {
            e.HasIndex(c => new { c.Name, c.State }).IsUnique();
        });
    }
}
