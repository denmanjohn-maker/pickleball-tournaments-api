using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PickleballTournaments.Api.Data;
using PickleballTournaments.Api.Domain;

namespace PickleballTournaments.Api.Scraping;

public class ScrapeSchedulerService(
    IServiceScopeFactory scopeFactory,
    ScrapeCoordinator coordinator,
    IOptions<ScrapingOptions> options,
    ILogger<ScrapeSchedulerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        if (!opts.Enabled)
        {
            logger.LogInformation("Scraping disabled via configuration.");
            return;
        }

        _ = ProcessQueueAsync(stoppingToken);

        await MaybeRunOnStartupAsync(opts, stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(opts.IntervalHours));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            coordinator.TryEnqueue(ScrapeTrigger.Scheduled, out _);
        }
    }

    private async Task MaybeRunOnStartupAsync(ScrapingOptions opts, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cutoff = DateTime.UtcNow.AddHours(-opts.IntervalHours);
        var hasRecentRun = await db.ScrapeRuns
            .AnyAsync(r => r.CompletedUtc != null && r.CompletedUtc >= cutoff, ct);

        if (!hasRecentRun)
            coordinator.TryEnqueue(ScrapeTrigger.Scheduled, out _);
    }

    private async Task ProcessQueueAsync(CancellationToken ct)
    {
        await foreach (var (trigger, runIdSource) in coordinator.Reader.ReadAllAsync(ct))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<ScrapeOrchestrator>();
                var run = await orchestrator.RunAsync(trigger, ct);
                runIdSource.TrySetResult(run.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scrape run failed unexpectedly.");
                runIdSource.TrySetException(ex);
            }
            finally
            {
                coordinator.MarkCompleted();
            }
        }
    }
}
