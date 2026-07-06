using Microsoft.EntityFrameworkCore;
using PickleballTournaments.Api.Data;
using PickleballTournaments.Api.Endpoints;
using PickleballTournaments.Api.Scraping;
using PickleballTournaments.Api.Scraping.AllPickleballTournaments;
using PickleballTournaments.Api.Scraping.PickleballBrackets;

const string UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.Configure<ScrapingOptions>(builder.Configuration.GetSection(ScrapingOptions.SectionName));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

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

app.MapTournamentEndpoints();
app.MapCityEndpoints();
app.MapScrapeEndpoints();

app.Run();

public partial class Program;
