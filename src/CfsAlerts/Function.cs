using Azure.Core;
using Mastonet.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

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
            var nextCheck = context.CurrentUtcDateTime.AddMinutes(5);
            await context.CreateTimer(nextCheck, CancellationToken.None);
        }

        // Perform more work here, or let the orchestration end.
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