# Pickleball Tournaments API

Read-only public data on U.S. pickleball tournaments, aggregated from multiple sources and deduplicated across them. Intended for use by client apps and LLM agents building on top of this data.

Base URL: wherever the API is deployed (e.g. `http://localhost:5000` locally). All responses are JSON. All JSON field names are `camelCase`.

Interactive OpenAPI docs are served at `/openapi/v1.json` (raw schema) and via Scalar UI when the API is running.

## Authentication

Only the `/api/admin/scrape/*` endpoints require auth. Everything else (`/api/tournaments*`, `/api/cities`, `/health`) is public, unauthenticated.

Admin endpoints check a `X-Api-Key` header against the server-configured `Scraping:AdminApiKey` value:

- If `AdminApiKey` is unset/empty on the server, auth is **not enforced** (any request passes).
- If it is set, the request must send `X-Api-Key: <value>` exactly matching it, or the server returns `401 Unauthorized` with an empty body.

As a data consumer you generally only need the public endpoints below.

## Data model

### Tournament

The core entity. Every scraped event is a `Tournament` row tied to one `source`. When the same real-world tournament is found on two sources, one row becomes the "canonical" record and the other points to it — see [Deduplication](#deduplication).

| Field | Type | Notes |
|---|---|---|
| `id` | int | |
| `name` | string | |
| `startDate` | date (`YYYY-MM-DD`) | |
| `endDate` | date or `null` | |
| `venueName` | string or `null` | |
| `city` | string | free-text city name as scraped, not necessarily a metro city name |
| `state` | string | 2-letter state code |
| `entryFee` | decimal or `null` | |
| `currency` | string or `null` | e.g. `"USD"` |
| `registrationUrl` | string or `null` | link to register, distinct from `sourceUrl` |
| `source` | string | enum name, one of `"PickleballBrackets"`, `"AllPickleballTournaments"` |
| `sourceUrl` | string | canonical link to the tournament's page on its source site |
| `isCanceled` | bool | |

`GET /api/tournaments/{id}` additionally returns:

| Field | Type | Notes |
|---|---|---|
| `relatedSourceListings` | array of the above tournament shape | other rows sharing the same dedup group (i.e. the same real-world event as scraped from other sources) |

### City

Represents one of the 50 seeded U.S. metro areas the scrapers search around (see `appsettings.json` → `Cities`), not the free-text `city` field on `Tournament`.

| Field | Type | Notes |
|---|---|---|
| `id` | int | |
| `name` | string | metro area name, e.g. `"New York"` |
| `state` | string | |
| `zip` | string | |
| `events1Month` | int | count of active (non-duplicate, non-canceled) tournaments starting within the next month, within this metro's radius |
| `events3Month` | int | same, within the next 3 months |

### ScrapeRun (admin only)

| Field | Type | Notes |
|---|---|---|
| `id` | int | |
| `startedUtc` | datetime | |
| `completedUtc` | datetime or `null` | |
| `trigger` | string | `"Scheduled"` or `"Manual"` |
| `status` | string | `"Running"`, `"Succeeded"`, `"PartialFailure"`, `"Failed"` |
| `bracketsFound` | int | tournaments found from the PickleballBrackets source |
| `aptFound` | int | tournaments found from the AllPickleballTournaments source |
| `inserted` | int | new rows written this run |
| `updated` | int | existing rows updated this run |
| `errorCount` | int | |
| `errorSummary` | string or `null` | |

### Deduplication

The same tournament can be listed on both source sites. When detected (same normalized name + start date + state), one row is marked canonical and the other gets `canonicalTournamentId` pointing to it internally. **List and lookup endpoints only ever return canonical rows** — duplicates are filtered out automatically, so as a consumer you don't need to dedupe client-side. To see the "other side" of a listing, fetch `GET /api/tournaments/{id}` and read `relatedSourceListings`.

## Endpoints

### `GET /api/tournaments`

Paginated, filterable list of tournaments. Only returns non-duplicate rows (canonical or singleton); canceled tournaments are excluded unless `includeCanceled=true`.

Query parameters (all optional):

| Param | Type | Default | Notes |
|---|---|---|---|
| `city` | string | — | exact match against a metro city's `name`. If the name doesn't match any known metro city, the response is an empty page (no error). |
| `state` | string | — | exact match against `Tournament.state`, case-insensitive |
| `from` | date | today (UTC) | start of date window |
| `to` | date | derived from `window` | end of date window |
| `window` | string | `"3m"` behavior | `"1m"` sets `to = from + 1 month`; anything else (or omitted, when `to` isn't given) sets `to = from + 3 months` |
| `radius` | number (miles) | `100` | only applies when `city` is given; filters by distance from that metro's center. Tournaments with no computed distance are included. |
| `source` | string | — | filters by source enum name (`"PickleballBrackets"` / `"AllPickleballTournaments"`), case-insensitive. Invalid or unrecognized values are silently ignored (no filter applied), not an error. |
| `includeCanceled` | bool | `false` | |
| `page` | int | `1` | clamped to `>= 1` |
| `pageSize` | int | `25` | clamped to `[1, 100]` |

Response `200 OK`:

```json
{
  "items": [ { "...tournament fields..." } ],
  "page": 1,
  "pageSize": 25,
  "totalCount": 137
}
```

### `GET /api/tournaments/{id}`

Single tournament by id, plus its cross-source duplicates.

- `200 OK` — tournament shape + `relatedSourceListings` (see [Tournament](#tournament) above).
- `404 Not Found` — no tournament with that id (empty body).

### `GET /api/cities`

All enabled metro cities the app tracks, with rolling event counts. No parameters. Not paginated — returns a plain array.

The `Cities` array in `src/PickleballTournaments.Api/appsettings.json` is the coverage inventory. It currently contains 50 geographically distributed U.S. metros. Startup synchronizes additions and coordinate or ZIP updates from this inventory, but intentionally leaves cities omitted from configuration enabled so existing city data and API filters remain available. Disable a city explicitly in the database when it should no longer be scraped.

Response `200 OK`:

```json
[
  { "id": 1, "name": "New York", "state": "NY", "zip": "10001", "events1Month": 12, "events3Month": 34 }
]
```

### `GET /health`

Plain liveness check. Returns `200 OK` with body `"Healthy"`. Not authenticated, not included in OpenAPI schema.

### Admin: `POST /api/admin/scrape`

Triggers a manual scrape run. Requires `X-Api-Key` header if `AdminApiKey` is configured server-side (see [Authentication](#authentication)). Fire-and-forget: returns immediately, doesn't wait for the scrape to finish.

- `202 Accepted` — `{ "message": "Scrape triggered." }`
- `409 Conflict` — `{ "message": "A scrape is already in progress." }` if a scrape is already running
- `401 Unauthorized` — bad/missing API key (empty body)

### Admin: `GET /api/admin/scrape/status`

Recent scrape run history. Requires the same auth as above.

Response `200 OK`:

```json
{
  "latest": { "...ScrapeRun fields... or null if none exist" },
  "recent": [ { "...ScrapeRun fields..." } ]
}
```

`recent` contains up to the 10 most recent runs, most recent first.

## Error responses

There is no standardized `ProblemDetails` error envelope in this API. Error responses are minimal and ad hoc:

| Status | When | Body |
|---|---|---|
| `404 Not Found` | tournament id doesn't exist | empty |
| `401 Unauthorized` | admin endpoint, bad/missing `X-Api-Key` | empty |
| `409 Conflict` | scrape already running | `{ "message": "..." }` |
| `500` | unhandled exception | framework default (no custom shape); may include a dev exception page in Development |

## Practical notes for building clients

- Always paginate `GET /api/tournaments` using `page`/`pageSize`/`totalCount` from the response — don't assume a fixed page size.
- The `city` filter only recognizes the 50 seeded metro area names from `GET /api/cities`; it does not do fuzzy or free-text city matching. To search "near a city", first look up its name via `GET /api/cities`.
- `source` values and enum fields (`source`, `trigger`, `status`) are always returned as their string names, never integers.
- Don't rely on `Tournament.city`/`state` free-text fields for metro-area grouping — use the `city` query parameter (which joins through the metro-city relationship) instead.
- Canceled tournaments are hidden by default; pass `includeCanceled=true` explicitly if you need them.
