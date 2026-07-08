# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project layout

The source lives under `src/PickleballTournaments.Api/` and tests under `tests/PickleballTournaments.Tests/`. The solution file is `PickleballTournaments.slnx`. The SDK version is pinned in `global.json` (currently .NET 10).

## Common commands

```bash
# Run the API (from repo root)
dotnet run --project src/PickleballTournaments.Api

# Run all tests
dotnet test

# Run a single test class
dotnet test --filter "FullyQualifiedName~JavaListEventsParserTests"

# Add an EF Core migration
dotnet ef migrations add <MigrationName> --project src/PickleballTournaments.Api

# Apply migrations manually
dotnet ef database update --project src/PickleballTournaments.Api
```

OpenAPI docs are served at `/openapi/v1.json` when the API is running.

## Architecture overview

**ASP.NET Core Minimal API** targeting net10, using SQLite via EF Core. No authentication except an `AdminApiKey` header for scrape-trigger endpoints.

### Domain

Three aggregate roots in `Domain/`:
- `Tournament` — scraped event with source-specific identity (`Source` + `SourceId`) and a `DedupKey` for cross-source deduplication. Duplicate rows from the other source point to a canonical row via `CanonicalTournamentId`.
- `MetroCity` — the set of metro areas the app scrapes around (seeded from `appsettings.json` → `Cities` array at startup via `SeedData.SyncCitiesAsync`).
- `ScrapeRun` — audit log for each scrape execution.
- `TournamentCity` join entity links tournaments to the metro cities they belong to, with an optional `DistanceMiles`.

### Scraping pipeline

The pipeline has three layers:

1. **`ITournamentScraper`** implementations (`AllPickleballTournamentsScraper`, `PickleballBracketsScraper`) — each scrapes one source for a given city/date window and returns `List<ScrapedTournament>`.
2. **`ScrapeOrchestrator`** — runs all scrapers concurrently per city, then serialises DB writes via a `SemaphoreSlim` (SQLite single-writer constraint). Handles upsert logic and cross-source dedup linking.
3. **`ScrapeCoordinator`** (singleton) + **`ScrapeSchedulerService`** (hosted service) — gate that prevents concurrent runs; scheduler fires on startup (if no recent run) and every `ScrapingOptions.IntervalHours`. Manual triggers go through `POST /api/scrape/trigger`.

Scraping config lives under the `Scraping` section in `appsettings.json` (`ScrapingOptions`).

### Endpoints

All endpoints use Minimal API route groups:
- `GET /api/tournaments` — paginated list with filters: `city`, `state`, `from`/`to` or `window` (1m/3m), `radius`, `source`, `includeCanceled`. City filtering joins through `TournamentCity`.
- `GET /api/tournaments/{id}` — single tournament with `RelatedSourceListings` (same dedup key, other source).
- `GET /api/cities` — list enabled metro cities with 1-month and 3-month event counts.
- `POST /api/scrape/trigger` — kicks off a scrape; requires `AdminApiKey` header. Returns the run id immediately via `TaskCompletionSource` hand-off.
- `GET /api/scrape/runs` — recent scrape run history.

### Data notes

- Tournaments with `CanonicalTournamentId != null` are duplicates and are filtered out from list queries automatically.
- `TournamentDedupService.BuildDedupKey` normalises the name (lowercase, strip non-alphanumeric), combines it with `StartDate` and `State`.
- `GeoService.DistanceMiles` uses the Haversine formula.

### Tests

xUnit + `Microsoft.AspNetCore.Mvc.Testing` for integration tests. Parser unit tests use fixture files in `tests/PickleballTournaments.Tests/Fixtures/`.
