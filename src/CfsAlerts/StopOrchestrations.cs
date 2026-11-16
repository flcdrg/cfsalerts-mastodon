using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;

namespace CfsAlerts
{
    public class StopOrchestrations
    {
        [Function("StopOrchestrations")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient durableTaskClient)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await foreach (var orchestration in durableTaskClient.GetAllInstancesAsync())
            {
                if (orchestration.RuntimeStatus != OrchestrationRuntimeStatus.Terminated)
                {
                    await response.WriteStringAsync($"Stopping instance {orchestration.InstanceId}");

                    await durableTaskClient.TerminateInstanceAsync(orchestration.InstanceId, CancellationToken.None);
                }
            }

            return response;
        }
    }
}
