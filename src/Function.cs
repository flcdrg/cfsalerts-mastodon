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

        string lastValue = string.Empty;
        while (true)
        {

            // Check CFS RSS feed.
            //var jobStatus = await context.CallActivityAsync<string>("GetJobStatus");
            //if (jobStatus == "Completed")
            //{
                // Perform an action when a condition is met.
                lastValue = await context.CallActivityAsync<string>("SendAlert", lastValue);
            //    break;
            //}

            // Orchestration sleeps until this time.
            var nextCheck = context.CurrentUtcDateTime.AddSeconds(10);
            await context.CreateTimer(nextCheck, CancellationToken.None);
        }

        // Perform more work here, or let the orchestration end.
    }

    [Function(nameof(SendAlert))]
    public static Task<string> SendAlert([ActivityTrigger] string lastValue, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(SendAlert));
        logger.LogInformation("Last time was {last}", lastValue);

        logger.LogInformation("Saying hello to {name}", DateTime.Now);

        return Task.FromResult($"Hello {DateTime.Now}");
    }
}