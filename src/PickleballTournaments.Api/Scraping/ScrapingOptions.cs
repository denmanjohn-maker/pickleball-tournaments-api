namespace PickleballTournaments.Api.Scraping;

public class ScrapingOptions
{
    public const string SectionName = "Scraping";

    public bool Enabled { get; set; } = true;
    public int IntervalHours { get; set; } = 12;
    public int RadiusMiles { get; set; } = 100;
    public int WindowMonths { get; set; } = 3;
    public int MinDelayMs { get; set; } = 1500;
    public int MaxDelayMs { get; set; } = 2500;
    public string AdminApiKey { get; set; } = "";
}
