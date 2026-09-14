using System.Globalization;
using System.Text.Json;
using Mastonet;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CfsAlerts;

public class CfsFunction
{
    private const string FeedUrl = "https://data.eso.sa.gov.au/feeds/prod/cfs_current_incidents.json";
    private const string DefaultIncidentLink = "https://www.cfs.sa.gov.au/incidents/";
    private static readonly string[] DateTimeFormats = ["d/M/yyyy H:mm", "d/M/yyyy H:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy HH:mm:ss"];

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

            response = await httpClient.GetStringAsync(FeedUrl);

            using var document = JsonDocument.Parse(response);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Feed did not return an array");

            foreach (var item in document.RootElement.EnumerateArray())
            {
                var incidentNumber = GetString(item, "IncidentNo");

                if (string.IsNullOrWhiteSpace(incidentNumber))
                    continue;

                var firstReportedDate = GetString(item, "Date");
                var firstReportedTime = GetString(item, "Time");

                if (!TryParsePubDate(firstReportedDate, firstReportedTime, out var pubDate))
                {
                    _logger.LogWarning("Skipping incident {incidentNumber} because its date '{date} {time}' could not be parsed",
                        incidentNumber, firstReportedDate, firstReportedTime);
                    continue;
                }

                newList.Add(new CfsFeedItem(
                    incidentNumber,
                    BuildTitle(GetString(item, "Location_name"), GetString(item, "Type")),
                    BuildDescription(firstReportedDate, firstReportedTime, GetString(item, "Status"), GetString(item, "FBD")),
                    GetString(item, "Message_link", DefaultIncidentLink),
                    pubDate
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

    private static string BuildDescription(string firstReportedDate, string firstReportedTime, string status, string region)
    {
        var parts = new List<string>();
        var firstReported = string.Join(" ", new[] { firstReportedDate, firstReportedTime }.Where(static value => !string.IsNullOrWhiteSpace(value)));

        if (!string.IsNullOrWhiteSpace(firstReported))
            parts.Add($"First Reported: {firstReported}");

        if (!string.IsNullOrWhiteSpace(status))
            parts.Add($"Status: {status}");

        if (!string.IsNullOrWhiteSpace(region))
            parts.Add($"Region: {region}");

        return string.Join("<br>", parts);
    }

    private static string BuildTitle(string locationName, string incidentType)
    {
        if (string.IsNullOrWhiteSpace(incidentType))
            return locationName;

        if (string.IsNullOrWhiteSpace(locationName))
            return incidentType;

        return $"{locationName} ({incidentType})";
    }

    private static string GetString(JsonElement item, string propertyName, string defaultValue = "")
    {
        if (!item.TryGetProperty(propertyName, out var property))
            return defaultValue;

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString()?.Trim() ?? defaultValue,
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => defaultValue
        };
    }

    private static bool TryParsePubDate(string firstReportedDate, string firstReportedTime, out DateTime dateTime)
    {
        var value = string.Join(" ", new[] { firstReportedDate, firstReportedTime }.Where(static text => !string.IsNullOrWhiteSpace(text)));

        return DateTime.TryParseExact(value, DateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dateTime);
    }
}