using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace CfsAlerts;

public class CheckFeed
{

    //[Function("CheckFeed")]
    //public static async Task TimerTriggerFunction([TimerTrigger("0 */5 * * * *", RunOnStartup = true)] TimerInfo myTimer, [DurableClient] DurableTaskClient client, FunctionContext executionContext)
    //{
    //    ILogger logger = executionContext.GetLogger(nameof(TimerTriggerFunction));

    //    string instanceId = await client.ScheduleNewOrchestrationInstanceAsync("MonitorJobStatus");
    //    logger.LogInformation("Created new orchestration with instance ID = {instanceId}", instanceId);

    //    if (myTimer.ScheduleStatus is not null)
    //    {
    //        logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
    //    }
    //}
}