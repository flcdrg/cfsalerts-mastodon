using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
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

            foreach (var item in xmlItems)
            {
                var dateTime = ParseDate(item);
                var title = FirstValue(item, "title", "headline", "event") ?? "Emergency alert";
                var description = FirstValue(item, "description", "summary", "instruction") ?? string.Empty;
                var link = FirstLink(item) ??
                           FirstValue(xml.Root, "link") ??
                           "https://www.cfs.sa.gov.au/incidents/";
                var id = FirstValue(item, "guid", "id", "identifier") ??
                         BuildStableId(item, title, dateTime);

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

    private static DateTime ParseDate(XElement item)
    {
        var possibleDate = FirstValue(item, "pubDate", "updated", "published", "sent", "effective", "onset");
        return DateTime.TryParse(possibleDate, out var parsedDate) ? parsedDate : DateTime.UtcNow;
    }

    private static string? FirstValue(XElement? element, params string[] names)
    {
        if (element is null)
            return null;

        foreach (var name in names)
        {
            var matchedElement = element.DescendantsAndSelf().FirstOrDefault(e => e.Name.LocalName == name);
            if (!string.IsNullOrWhiteSpace(matchedElement?.Value))
                return matchedElement.Value.Trim();
        }

        return null;
    }

    private static string? FirstLink(XElement item)
    {
        var linkElement = item.DescendantsAndSelf().FirstOrDefault(e => e.Name.LocalName == "link");
        if (linkElement is null)
            return null;

        if (!string.IsNullOrWhiteSpace(linkElement.Value))
            return linkElement.Value.Trim();

        var href = linkElement.Attribute("href")?.Value;
        return string.IsNullOrWhiteSpace(href) ? null : href.Trim();
    }

    private static string BuildStableId(XElement item, string title, DateTime dateTime)
    {
        var rawValue = $"{title}|{dateTime:O}|{item}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));
        return Convert.ToHexString(hash);
    }
}