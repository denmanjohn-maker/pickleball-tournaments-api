using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PickleballTournaments.Api.Data;
using PickleballTournaments.Api.Endpoints;
using PickleballTournaments.Api.Scraping;
using PickleballTournaments.Api.Scraping.AllPickleballTournaments;
using PickleballTournaments.Api.Scraping.PickleballBrackets;

const string UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

var builder = WebApplication.CreateBuilder(args);

// Railway injects a dynamic PORT env var and routes traffic to it; unset locally, so this is a no-op in dev.
var portValue = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(portValue))
{
    if (!int.TryParse(portValue, out var port) || port is <= 0 or > 65535)
    {
        throw new InvalidOperationException($"PORT environment variable is set to an invalid value: '{portValue}'.");
    }

    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddOpenApi();
builder.Services.Configure<ScrapingOptions>(builder.Configuration.GetSection(ScrapingOptions.SectionName));

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Default configuration.");

// SQLite creates the .db file itself but not missing parent directories (e.g. a Railway volume mount).
var sqliteDirectory = Path.GetDirectoryName(new SqliteConnectionStringBuilder(connectionString).DataSource);
if (!string.IsNullOrEmpty(sqliteDirectory))
{
    Directory.CreateDirectory(sqliteDirectory);
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

builder.Services.AddHttpClient(AllPickleballTournamentsScraper.HttpClientName, client =>
{
    client.BaseAddress = new Uri("https://www.allpickleballtournaments.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
}).AddStandardResilienceHandler();

builder.Services.AddHttpClient(PickleballBracketsScraper.HttpClientName, client =>
{
    client.BaseAddress = new Uri("https://pickleballtournaments.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
}).AddStandardResilienceHandler();

builder.Services.AddSingleton<ServerActionDiscovery>();
builder.Services.AddScoped<ITournamentScraper, AllPickleballTournamentsScraper>();
builder.Services.AddScoped<ITournamentScraper, PickleballBracketsScraper>();
builder.Services.AddScoped<ScrapeOrchestrator>();
builder.Services.AddSingleton<ScrapeCoordinator>();
builder.Services.AddHostedService<ScrapeSchedulerService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.SyncCitiesAsync(db, app.Configuration);
}

app.MapOpenApi();

app.MapGet("/health", () => Results.Ok("Healthy")).ExcludeFromDescription();

app.MapTournamentEndpoints();
app.MapCityEndpoints();
app.MapScrapeEndpoints();

app.Run();

public partial class Program;
