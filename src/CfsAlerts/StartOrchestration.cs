using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace CfsAlerts
{
    public class StartOrchestration
    {
        private readonly ILogger<StartOrchestration> _logger;

        public StartOrchestration(ILogger<StartOrchestration> logger)
        {
            _logger = logger;
        }

        [Function("StartOrchestration")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient durableTaskClient)
        {
            const string instanceId = "MonitorJobStatus 1B5BF432-5DEA-481D-AE45-32AB347C8F2F";
            var existing = await durableTaskClient.GetInstanceAsync(instanceId);

#pragma warning disable CS0618 // Protect a legacy instance transitioning through ContinueAsNew.
            if (existing?.RuntimeStatus is OrchestrationRuntimeStatus.Running
                or OrchestrationRuntimeStatus.Pending
                or OrchestrationRuntimeStatus.ContinuedAsNew
                or OrchestrationRuntimeStatus.Suspended)
            {
                _logger.LogInformation("Orchestration {instanceId} already exists with status {status}", instanceId, existing.RuntimeStatus);
            }
            else
            {
                var options = new StartOrchestrationOptions(InstanceId: instanceId).WithDedupeStatuses(
                    OrchestrationRuntimeStatus.Running,
                    OrchestrationRuntimeStatus.Pending,
                    OrchestrationRuntimeStatus.ContinuedAsNew,
                    OrchestrationRuntimeStatus.Suspended);
                await durableTaskClient.ScheduleNewOrchestrationInstanceAsync(nameof(MonitorJobStatus), new List<CfsFeedItem>(), options);
                _logger.LogInformation("Started orchestration {instanceId}", instanceId);
            }
#pragma warning restore CS0618

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await response.WriteStringAsync("Welcome to Azure Functions!");

            return response;
        }
    }
}
