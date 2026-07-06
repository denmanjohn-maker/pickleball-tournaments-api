namespace PickleballTournaments.Api.Domain;

public enum ScrapeTrigger
{
    Scheduled = 1,
    Manual = 2,
}

public enum ScrapeStatus
{
    Running = 1,
    Succeeded = 2,
    PartialFailure = 3,
    Failed = 4,
}

public class ScrapeRun
{
    public int Id { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public ScrapeTrigger Trigger { get; set; }
    public ScrapeStatus Status { get; set; }
    public int BracketsFound { get; set; }
    public int AptFound { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int ErrorCount { get; set; }
    public string? ErrorSummary { get; set; }
}
