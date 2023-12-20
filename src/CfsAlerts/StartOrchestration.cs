using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;

namespace CfsAlerts
{
    public class StartOrchestration
    {
        private readonly DurableTaskClient _durableTaskClient;

        public StartOrchestration(DurableTaskClient durableTaskClient)
        {
            _durableTaskClient = durableTaskClient;
        }

        [Function("StartOrchestration")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            await _durableTaskClient.ScheduleNewOrchestrationInstanceAsync("MonitorJobStatus");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await response.WriteStringAsync("Welcome to Azure Functions!");

            return response;
        }
    }
}
