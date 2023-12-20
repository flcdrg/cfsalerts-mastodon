using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace CfsAlerts;

public static class MonitorJobStatus
{
    [Function(nameof(MonitorJobStatus))]
    public static async Task Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var lastValue = new List<CfsFeedItem>();
        while (true)
        {
            lastValue = await context.CallActivityAsync<List<CfsFeedItem>>(nameof(CfsFunction.CheckAlerts), lastValue);

            // Orchestration sleeps until this time (TTL is 15 minutes in RSS)
            var nextCheck = context.CurrentUtcDateTime.AddMinutes(15);
            await context.CreateTimer(nextCheck, CancellationToken.None);
        }

        // Perform more work here, or let the orchestration end.
    }
}