using Microsoft.EntityFrameworkCore;
using PickleballTournaments.Api.Data;
using PickleballTournaments.Api.Domain;

namespace PickleballTournaments.Api.Endpoints;

public static class TournamentEndpoints
{
    public static void MapTournamentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tournaments").WithTags("Tournaments");

        group.MapGet("/", async (
            AppDbContext db,
            string? city,
            string? state,
            string? window,
            DateOnly? from,
            DateOnly? to,
            double? radius,
            string? source,
            bool includeCanceled = false,
            int page = 1,
            int pageSize = 25) =>
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var effectiveFrom = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var effectiveTo = to ?? effectiveFrom.AddMonths(window == "1m" ? 1 : 3);

            var query = db.Tournaments.AsNoTracking()
                .Where(t => t.CanonicalTournamentId == null)
                .Where(t => t.StartDate >= effectiveFrom && t.StartDate <= effectiveTo);

            if (!includeCanceled)
                query = query.Where(t => !t.IsCanceled);

            if (!string.IsNullOrWhiteSpace(state))
                query = query.Where(t => t.State == state.ToUpperInvariant());

            if (!string.IsNullOrWhiteSpace(source) &&
                Enum.TryParse<TournamentSource>(source, ignoreCase: true, out var parsedSource))
                query = query.Where(t => t.Source == parsedSource);

            if (!string.IsNullOrWhiteSpace(city))
            {
                var metro = await db.MetroCities.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Name == city);
                if (metro is null)
                    return Results.Ok(new PagedResult<TournamentDto>([], page, pageSize, 0));

                var effectiveRadius = radius ?? 100;
                var tournamentIds = db.TournamentCities.AsNoTracking()
                    .Where(tc => tc.MetroCityId == metro.Id &&
                        (tc.DistanceMiles == null || tc.DistanceMiles <= effectiveRadius))
                    .Select(tc => tc.TournamentId);
                query = query.Where(t => tournamentIds.Contains(t.Id));
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderBy(t => t.StartDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => TournamentDto.From(t))
                .ToListAsync();

            return Results.Ok(new PagedResult<TournamentDto>(items, page, pageSize, totalCount));
        });

        group.MapGet("/{id:int}", async (AppDbContext db, int id) =>
        {
            var tournament = await db.Tournaments.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);
            if (tournament is null)
                return Results.NotFound();

            var canonicalId = tournament.CanonicalTournamentId ?? tournament.Id;
            var related = await db.Tournaments.AsNoTracking()
                .Where(t => t.Id != id && (t.Id == canonicalId || t.CanonicalTournamentId == canonicalId))
                .Select(t => TournamentDto.From(t))
                .ToListAsync();

            var dto = new TournamentDetailDto(
                tournament.Id, tournament.Name, tournament.StartDate, tournament.EndDate,
                tournament.VenueName, tournament.City, tournament.State,
                tournament.EntryFee, tournament.Currency, tournament.RegistrationUrl,
                tournament.Source.ToString(), tournament.SourceUrl, tournament.IsCanceled,
                related);

            return Results.Ok(dto);
        });
    }
}
