using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using PickleballTournaments.Api.Domain;

namespace PickleballTournaments.Api.Scraping.AllPickleballTournaments;

public record JavaListEventsResult(
    IReadOnlyList<ScrapedTournament> Tournaments,
    IReadOnlyList<int> AdditionalPages);

/// <summary>
/// Parses the javaListEvents.asp response: a JavaScript snippet that assigns HTML strings
/// to mainSearchResults / searchPagination via innerHTML.
/// </summary>
public static partial class JavaListEventsParser
{
    private const string BaseUrl = "https://www.allpickleballtournaments.com/";

    [GeneratedRegex(@"document\.getElementById\('mainSearchResults'\)\.innerHTML\s*=\s*'(.*?)';", RegexOptions.Singleline)]
    private static partial Regex MainResultsRegex();

    [GeneratedRegex(@"document\.getElementById\('searchPagination'\)\.innerHTML\s*=\s*'(.*?)';", RegexOptions.Singleline)]
    private static partial Regex PaginationRegex();

    [GeneratedRegex(@"loadPage\((\d+)\)")]
    private static partial Regex LoadPageRegex();

    [GeneratedRegex(@"TID=(\d+)")]
    private static partial Regex TidRegex();

    // "City, ST USA" or "City, ST, USA"
    [GeneratedRegex(@"^(?<city>.+?),\s*(?<state>[A-Z]{2})\b")]
    private static partial Regex CityStateRegex();

    // "10-12", "10", "31-2"
    [GeneratedRegex(@"(?<start>\d{1,2})(?:\s*-\s*(?<end>\d{1,2}))?")]
    private static partial Regex DayRangeRegex();

    public static JavaListEventsResult Parse(string response, DateOnly windowFrom, DateOnly windowTo)
    {
        // The first innerHTML assignment is a "Loading Events" placeholder; the last carries results.
        var htmlMatches = MainResultsRegex().Matches(response);
        if (htmlMatches.Count == 0)
            return new JavaListEventsResult([], []);

        var html = Unescape(htmlMatches[^1].Groups[1].Value);
        var parser = new HtmlParser();
        var doc = parser.ParseDocument(html);

        var tournaments = new List<ScrapedTournament>();
        foreach (var link in doc.QuerySelectorAll("a[href*='event-details.asp?TID=']"))
        {
            var href = link.GetAttribute("href") ?? "";
            var tidMatch = TidRegex().Match(href);
            var name = link.TextContent.Trim();
            if (!tidMatch.Success || name.Length == 0)
                continue;

            var tid = tidMatch.Groups[1].Value;
            if (tournaments.Any(t => t.SourceId == tid))
                continue;

            // Walk up to the row containing this link to find the date cell and location line.
            var row = FindRow(link);
            if (row is null)
                continue;

            var dateCell = row.QuerySelector("h5.month") ?? row.QuerySelector(".month");
            var (start, end) = ParseDateCell(dateCell?.InnerHtml ?? "", windowFrom, windowTo);
            if (start is null)
                continue;

            string city = "", state = "";
            foreach (var p in row.QuerySelectorAll("p"))
            {
                var m = CityStateRegex().Match(p.TextContent.Trim());
                if (m.Success)
                {
                    city = m.Groups["city"].Value.Trim();
                    state = m.Groups["state"].Value;
                    break;
                }
            }
            if (city.Length == 0)
                continue;

            tournaments.Add(new ScrapedTournament
            {
                Name = name,
                StartDate = start.Value,
                EndDate = end,
                City = city,
                State = state,
                Source = TournamentSource.AllPickleballTournaments,
                SourceId = tid,
                SourceUrl = BaseUrl + "event-details.asp?TID=" + tid,
                RegistrationUrl = BaseUrl + "event-details.asp?TID=" + tid,
            });
        }

        var pages = new List<int>();
        var pagMatches = PaginationRegex().Matches(response);
        if (pagMatches.Count > 0)
        {
            var pagHtml = Unescape(pagMatches[^1].Groups[1].Value);
            foreach (Match m in LoadPageRegex().Matches(pagHtml))
            {
                var page = int.Parse(m.Groups[1].Value);
                if (!pages.Contains(page))
                    pages.Add(page);
            }
        }

        return new JavaListEventsResult(tournaments, pages);
    }

    private static AngleSharp.Dom.IElement? FindRow(AngleSharp.Dom.IElement link)
    {
        // The listing groups a date cell, title link, and location under a table row or div block.
        for (var el = link.ParentElement; el is not null; el = el.ParentElement)
        {
            if (el is IHtmlTableRowElement)
                return el;
            if (el.QuerySelector("h5.month") is not null || el.QuerySelector(".month") is not null)
                return el;
        }
        return link.ParentElement;
    }

    /// <summary>
    /// Parses a date cell like "Jul&lt;br&gt;10-12". The cell has no year, so it is resolved
    /// against the request window (handles Dec-&gt;Jan rollover).
    /// </summary>
    internal static (DateOnly? Start, DateOnly? End) ParseDateCell(string cellHtml, DateOnly windowFrom, DateOnly windowTo)
    {
        var parts = Regex.Split(cellHtml, @"<br\s*/?>", RegexOptions.IgnoreCase);
        if (parts.Length < 2)
            return (null, null);

        var monthText = Regex.Replace(parts[0], "<.*?>", "").Trim();
        if (!DateTime.TryParseExact(monthText, "MMM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthDate))
            return (null, null);
        var month = monthDate.Month;

        var dayText = Regex.Replace(parts[1], "<.*?>", "").Trim();
        var dayMatch = DayRangeRegex().Match(dayText);
        if (!dayMatch.Success)
            return (null, null);

        var startDay = int.Parse(dayMatch.Groups["start"].Value);
        var endDay = dayMatch.Groups["end"].Success ? int.Parse(dayMatch.Groups["end"].Value) : startDay;

        // Pick the year that lands the start date inside (or nearest to) the search window.
        var year = ResolveYear(month, startDay, windowFrom, windowTo);
        if (year is null)
            return (null, null);

        var start = new DateOnly(year.Value, month, Math.Min(startDay, DateTime.DaysInMonth(year.Value, month)));

        DateOnly end;
        if (endDay >= startDay)
        {
            end = new DateOnly(year.Value, month, Math.Min(endDay, DateTime.DaysInMonth(year.Value, month)));
        }
        else
        {
            // Range crosses into the next month, e.g. "31-2".
            var endMonthDate = start.AddMonths(1);
            end = new DateOnly(endMonthDate.Year, endMonthDate.Month, Math.Min(endDay, DateTime.DaysInMonth(endMonthDate.Year, endMonthDate.Month)));
        }

        return (start, end);
    }

    private static int? ResolveYear(int month, int day, DateOnly windowFrom, DateOnly windowTo)
    {
        foreach (var year in new[] { windowFrom.Year, windowFrom.Year + 1, windowTo.Year })
        {
            if (day > DateTime.DaysInMonth(year, month))
                continue;
            var candidate = new DateOnly(year, month, day);
            // Tolerate a few days of slack for multi-day events straddling the window edges.
            if (candidate >= windowFrom.AddDays(-7) && candidate <= windowTo.AddDays(7))
                return year;
        }
        return null;
    }

    private static string Unescape(string js) => js.Replace(@"\'", "'").Replace(@"\/", "/").Replace(@"\\", @"\");
}
