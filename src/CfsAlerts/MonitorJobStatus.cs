using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace CfsAlerts;

public static class MonitorJobStatus
{
    // Follows the 'Eternal orchestration' pattern
    // https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-eternal-orchestrations?WT.mc_id=DOP-MVP-5001655
    [Function(nameof(MonitorJobStatus))]
    public static async Task Run(
        [OrchestrationTrigger] TaskOrchestrationContext context, List<CfsFeedItem> lastValue)
    {
        var newValue = await context.CallActivityAsync<List<CfsFeedItem>>(nameof(CfsFunction.CheckAlerts), lastValue);

#if RELEASE
        // Orchestration sleeps until this time (TTL is 15 minutes in RSS)
        var nextCheck = context.CurrentUtcDateTime.AddMinutes(15);
#else
        var nextCheck = context.CurrentUtcDateTime.AddSeconds(20);
#endif

        await context.CreateTimer(nextCheck, CancellationToken.None);

        context.ContinueAsNew(newValue);
    }
}