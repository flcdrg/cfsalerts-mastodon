using System.Xml.Linq;
using Mastonet;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CfsAlerts;

public class CfsFunction
{
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
        using var httpClient = _httpClientFactory.CreateClient();

        var response = await httpClient.GetStringAsync("https://data.eso.sa.gov.au/prod/cfs/criimson/cfs_current_incidents.xml");

        var xml = XDocument.Parse(response);

        if (xml.Root.Element("channel") is null)
            throw new Exception("No channel element found in feed");

        var xmlItems = xml.Root.Element("channel").Elements("item").ToList();

        var newList = new List<CfsFeedItem>();

        var now = DateTime.Now;

        foreach (XElement item in xmlItems)
        {
            var dateTime = DateTime.Parse(item.Element("pubDate").Value);

            if (dateTime < now)
            {
                newList.Add(new CfsFeedItem(
                    item.Element("guid").Value,
                    item.Element("title").Value,
                    item.Element("description").Value,
                    item.Element("link").Value,
                    dateTime
                ));
            }
        }

        // Find items in newList that are not in oldList
        var newItems = newList.Except(oldList).ToList();

        if (newItems.Any())
        {
            var accessToken = _settings.Token;
            var client = new MastodonClient(_settings.Instance, accessToken);
            
            foreach (var item in newItems)
            {
                string message = $"{item.Title}\n\n{item.Description.Replace("<br>", "\n")}\n{item.Link}";

                _logger.LogWarning("Tooting: {item}", message);

                await client.PublishStatus(message, Visibility.Private);
            }
        }

        return newList;
    }
}