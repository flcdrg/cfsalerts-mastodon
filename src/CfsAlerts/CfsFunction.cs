using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using System.Globalization;
using Mastonet;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CfsAlerts;

public class CfsFunction
{
    private const string AlertFeedUrl = "https://data.eso.sa.gov.au/feeds/prod/cap-au.xml";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CfsFunction> _logger;
    private readonly MastodonSettings _settings;

    // ReSharper disable once ConvertToPrimaryConstructor - not supported by Azure Functions
    public CfsFunction(IHttpClientFactory httpClientFactory,
        ILogger<CfsFunction> logger,
        IOptions<MastodonSettings> settings)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    [Function(nameof(CheckAlerts))]
    public async Task<List<CfsFeedItem>> CheckAlerts([ActivityTrigger] List<CfsFeedItem> oldList)
    {
        var newList = new List<CfsFeedItem>();

        var response = string.Empty;
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();

            response = await httpClient.GetStringAsync(AlertFeedUrl);

            var xml = XDocument.Parse(response);
            var xmlItems = GetAlertItems(xml).ToList();
            var feedDate = ParseDate(xml.Root);
            var feedLink = FirstLink(xml.Root);

            foreach (var item in xmlItems)
            {
                var dateTime = ParseDate(item) ?? feedDate ?? DateTime.MinValue;
                var title = FirstValue(item, false, "title") ??
                            FirstValue(item, true, "headline", "event") ??
                            "Emergency alert";
                var description = FirstValue(item, false, "description", "summary") ??
                                  FirstValue(item, true, "description", "instruction") ??
                                  string.Empty;
                var link = FirstLink(item) ??
                           feedLink ??
                           "https://www.cfs.sa.gov.au/incidents/";
                var id = FirstValue(item, false, "guid", "id", "identifier") ??
                         FirstValue(item, true, "identifier") ??
                         BuildStableId(title, link, dateTime);

                newList.Add(new CfsFeedItem(
                    id,
                    title,
                    description,
                    link,
                    dateTime
                ));
            }

            // Find items in newList that are not in oldList
            var newItems = newList.Except(oldList).ToList();

            if (newItems.Any())
            {
                var accessToken = _settings.Token;
                var client = new MastodonClient(_settings.Instance, accessToken);

                foreach (var item in newItems)
                {
                    var message = $"{item.Title}\n\n{item.Description.Replace("<br>", "\n")}\n{item.Link}";

                    _logger.LogInformation("Tooting: {item}", message);

#if RELEASE
                await client.PublishStatus(message, Visibility.Unlisted);
#endif
                }
            }
            else
            {
                _logger.LogInformation("No new items found");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Problems. Data: {data}", response);
        }

        return newList;
    }

    private static IEnumerable<XElement> GetAlertItems(XDocument xml)
    {
        if (xml.Root is null)
            return [];

        if (xml.Root.Name.LocalName == "alert")
            return [xml.Root];

        var items = xml.Descendants().Where(e => e.Name.LocalName is "item" or "entry").ToList();
        return items;
    }

    private static DateTime? ParseDate(XElement? item)
    {
        var directDate = FirstValue(item, false, "pubDate", "updated", "published");
        if (DateTime.TryParse(directDate, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var directParsedDate))
            return directParsedDate;

        var nestedDate = FirstValue(item, true, "sent", "effective", "onset", "updated", "published");
        return DateTime.TryParse(nestedDate, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var nestedParsedDate) ? nestedParsedDate : null;
    }

    private static string? FirstValue(XElement? element, bool includeDescendants, params string[] names)
    {
        if (element is null)
            return null;

        foreach (var name in names)
        {
            var matchedElement = element.Elements().FirstOrDefault(e => e.Name.LocalName == name);
            if (!string.IsNullOrWhiteSpace(matchedElement?.Value))
                return matchedElement.Value.Trim();

            if (!includeDescendants)
                continue;

            matchedElement = element.Descendants().FirstOrDefault(e => e.Name.LocalName == name);
            if (!string.IsNullOrWhiteSpace(matchedElement?.Value))
                return matchedElement.Value.Trim();
        }

        return null;
    }

    private static string? FirstLink(XElement? item)
    {
        if (item is null)
            return null;

        var linkElements = item.Elements().Where(e => e.Name.LocalName == "link").ToList();
        if (linkElements.Count == 0)
            return null;

        var preferredLink = linkElements
            .FirstOrDefault(link =>
                string.IsNullOrWhiteSpace(link.Attribute("rel")?.Value) ||
                string.Equals(link.Attribute("rel")?.Value, "alternate", StringComparison.OrdinalIgnoreCase))
            ?? linkElements.First();

        var href = preferredLink.Attribute("href")?.Value;
        if (!string.IsNullOrWhiteSpace(href))
            return href.Trim();

        return string.IsNullOrWhiteSpace(preferredLink.Value) ? null : preferredLink.Value.Trim();
    }

    private static string BuildStableId(string title, string link, DateTime dateTime)
    {
        var rawValue = $"{title}|{link}|{dateTime:O}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));
        return Convert.ToHexString(hash);
    }
}