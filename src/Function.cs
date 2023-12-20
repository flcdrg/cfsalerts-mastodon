using System.Xml.Linq;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace CfsAlerts;

public static class Function
{
    [Function("MonitorJobStatus")]
    public static async Task Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        int pollingInterval = (int)TimeSpan.FromMinutes(1).TotalSeconds;
        //DateTime expiryTime = GetExpiryTime();

        var lastValue = new List<CfsFeedItem>();
        while (true)
        {
            lastValue = await context.CallActivityAsync<List<CfsFeedItem>>(nameof(CfsFunction.CheckAlerts), lastValue);

            // Orchestration sleeps until this time.
            var nextCheck = context.CurrentUtcDateTime.AddSeconds(60);
            await context.CreateTimer(nextCheck, CancellationToken.None);
        }

        // Perform more work here, or let the orchestration end.
    }
}

public class CfsFunction(IHttpClientFactory httpClientFactory)
{
    [Function(nameof(CheckAlerts))]
    public async Task<List<CfsFeedItem>> CheckAlerts([ActivityTrigger] List<CfsFeedItem> oldList, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(CheckAlerts));

        using var httpClient = httpClientFactory.CreateClient();

        var response = await httpClient.GetStringAsync("https://data.eso.sa.gov.au/prod/cfs/criimson/cfs_current_incidents.xml");

        var xml = XDocument.Parse(response);

        var xmlItems = xml.Root.Element("channel").Elements("item").ToList();

        var newList = new List<CfsFeedItem>();

        foreach (XElement item in xmlItems)
        {
            //logger.LogInformation("pubDate {date}", DateTime.Parse(item.Element("pubDate").Value));

            newList.Add(new CfsFeedItem(
                item.Element("guid").Value,
                item.Element("title").Value,
                item.Element("description").Value,
                item.Element("link").Value,
                DateTime.Parse(item.Element("pubDate").Value)
            ));
        }

        // Find items in newList that are not in oldList
        var newItems = newList.Except(oldList);

        foreach (var newItem in newItems)
        {
            logger.LogInformation("New item: {item}", newItem);
        }

        logger.LogTrace(xml.ToString());

        return newList;
    }
}

public record CfsFeedItem(string Id, string Title, string Description, string Link, DateTime PubDate)
{
    public virtual bool Equals(CfsFeedItem? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}