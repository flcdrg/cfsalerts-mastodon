using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;

namespace CfsAlerts
{
    public class StopOrchestrations
    {
        private readonly DurableTaskClient _durableTaskClient;

        public StopOrchestrations(DurableTaskClient durableTaskClient)
        {
            _durableTaskClient = durableTaskClient;
        }

        [Function("StopOrchestrations")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await foreach (var orchestration in _durableTaskClient.GetAllInstancesAsync())
            {
                if (orchestration.RuntimeStatus != OrchestrationRuntimeStatus.Terminated)
                {
                    await response.WriteStringAsync($"Stopping instance {orchestration.InstanceId}");

                    await _durableTaskClient.TerminateInstanceAsync(orchestration.InstanceId, CancellationToken.None);
                }
            }

            return response;
        }
    }
}
