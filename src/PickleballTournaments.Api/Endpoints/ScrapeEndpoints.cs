using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PickleballTournaments.Api.Data;
using PickleballTournaments.Api.Domain;
using PickleballTournaments.Api.Scraping;

namespace PickleballTournaments.Api.Endpoints;

public static class ScrapeEndpoints
{
    public static void MapScrapeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/scrape").WithTags("Admin").AddEndpointFilter(AdminAuthFilter);

        group.MapPost("/", async (ScrapeCoordinator coordinator, HttpContext ctx, CancellationToken ct) =>
        {
            if (!coordinator.TryEnqueue(ScrapeTrigger.Manual, out var runIdSource))
                return Results.Conflict(new { message = "A scrape is already in progress." });

            // Return as soon as the run is recorded (has an id); don't block on full completion.
            _ = runIdSource.Task;
            await Task.Delay(50, ct); // brief yield so the orchestrator has a moment to create the ScrapeRun row
            return Results.Accepted(value: new { message = "Scrape triggered." });
        });

        group.MapGet("/status", async (AppDbContext db) =>
        {
            var runs = await db.ScrapeRuns.AsNoTracking()
                .OrderByDescending(r => r.StartedUtc)
                .Take(10)
                .Select(r => ScrapeRunDto.From(r))
                .ToListAsync();

            return Results.Ok(new { latest = runs.FirstOrDefault(), recent = runs });
        });
    }

    private static async ValueTask<object?> AdminAuthFilter(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var opts = ctx.HttpContext.RequestServices.GetRequiredService<IOptions<ScrapingOptions>>().Value;
        if (string.IsNullOrEmpty(opts.AdminApiKey))
            return await next(ctx);

        if (!ctx.HttpContext.Request.Headers.TryGetValue("X-Api-Key", out var key) || key != opts.AdminApiKey)
            return Results.Unauthorized();

        return await next(ctx);
    }
}
