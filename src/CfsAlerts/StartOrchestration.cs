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
        private readonly DurableTaskClient _durableTaskClient;
        private readonly ILogger<StartOrchestration> _logger;

        public StartOrchestration(DurableTaskClient durableTaskClient, ILogger<StartOrchestration> logger)
        {
            _durableTaskClient = durableTaskClient;
            _logger = logger;
        }

        [Function("StartOrchestration")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("Starting orchestration");

            // This should ensure we only have a single orchestration running at a time
            const string instanceId = "MonitorJobStatus 1B5BF432-5DEA-481D-AE45-32AB347C8F2F";

            try
            {
                await _durableTaskClient.ScheduleNewOrchestrationInstanceAsync("MonitorJobStatus", new StartOrchestrationOptions( InstanceId: instanceId));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ScheduleNewOrchestrationInstanceAsync failed - maybe it was already running?");
            }
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await response.WriteStringAsync("Welcome to Azure Functions!");

            return response;
        }
    }
}
