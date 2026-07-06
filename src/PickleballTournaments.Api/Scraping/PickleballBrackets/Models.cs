using System.Text.Json.Serialization;

namespace PickleballTournaments.Api.Scraping.PickleballBrackets;

public record GetTournamentsRequest
{
    [JsonPropertyName("keyword")] public string Keyword { get; init; } = "";
    [JsonPropertyName("locationType")] public string LocationType { get; init; } = "manual";
    [JsonPropertyName("placeKeyword")] public required string PlaceKeyword { get; init; }
    [JsonPropertyName("center")] public required LatLng Center { get; init; }
    [JsonPropertyName("bounds")] public required Bounds Bounds { get; init; }
    [JsonPropertyName("googlePlaceId")] public string GooglePlaceId { get; init; } = "";
    [JsonPropertyName("dateFrom")] public required string DateFrom { get; init; }
    [JsonPropertyName("dateTo")] public required string DateTo { get; init; }
    [JsonPropertyName("pastTours")] public bool PastTours { get; init; }
    [JsonPropertyName("zoomLevel")] public int ZoomLevel { get; init; } = 8;
    [JsonPropertyName("currentPage")] public int CurrentPage { get; init; } = 1;
    [JsonPropertyName("tournamentFilter")] public string TournamentFilter { get; init; } = "local";
    [JsonPropertyName("partner")] public string Partner { get; init; } = "";
    [JsonPropertyName("clubId")] public string ClubId { get; init; } = "";
    [JsonPropertyName("userLocation")] public required LatLng UserLocation { get; init; }
}

public record LatLng(
    [property: JsonPropertyName("lat")] double Lat,
    [property: JsonPropertyName("lng")] double Lng);

public record Bounds(
    [property: JsonPropertyName("ne_lat")] double NeLat,
    [property: JsonPropertyName("ne_lng")] double NeLng,
    [property: JsonPropertyName("sw_lat")] double SwLat,
    [property: JsonPropertyName("sw_lng")] double SwLng);

public record GetTournamentsResponse
{
    [JsonPropertyName("data")] public TournamentsData? Data { get; init; }
    [JsonPropertyName("statusCode")] public int StatusCode { get; init; }
}

public record TournamentsData
{
    [JsonPropertyName("items")] public List<TournamentItem> Items { get; init; } = [];
    [JsonPropertyName("totalCount")] public int TotalCount { get; init; }
    [JsonPropertyName("foundAtRadiusInKm")] public double? FoundAtRadiusInKm { get; init; }
}

public record TournamentItem
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("title")] public required string Title { get; init; }
    [JsonPropertyName("slug")] public string? Slug { get; init; }
    [JsonPropertyName("dateFrom")] public DateTime DateFrom { get; init; }
    [JsonPropertyName("dateTo")] public DateTime? DateTo { get; init; }
    [JsonPropertyName("location")] public string? Location { get; init; }
    [JsonPropertyName("price")] public decimal? Price { get; init; }
    [JsonPropertyName("currency")] public string? Currency { get; init; }
    [JsonPropertyName("lat")] public double Lat { get; init; }
    [JsonPropertyName("lng")] public double Lng { get; init; }
    [JsonPropertyName("isCanceled")] public bool IsCanceled { get; init; }
    [JsonPropertyName("registrationCount")] public int RegistrationCount { get; init; }
}
