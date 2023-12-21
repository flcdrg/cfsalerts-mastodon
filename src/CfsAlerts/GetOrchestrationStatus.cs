using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;

namespace CfsAlerts
{
    public class GetOrchestrationStatus
    {
        private readonly DurableTaskClient _durableTaskClient;

        public GetOrchestrationStatus(DurableTaskClient durableTaskClient)
        {
            _durableTaskClient = durableTaskClient;
        }

        [Function("GetOrchestrationStatus")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<html><head><title>Orchestration Status</title></head><body>");
            sb.AppendLine("<h1>Orchestrations</h1>");
            sb.AppendLine("<ul>");
            await foreach (var orchestration  in _durableTaskClient.GetAllInstancesAsync()) {
                sb.AppendLine($"<li>{orchestration.InstanceId} - {orchestration.Name} - {orchestration.RuntimeStatus}</li>");
            }
            sb.AppendLine("</ul>");
            sb.AppendLine("</body></html>");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/html; charset=utf-8");

            await response.WriteStringAsync(sb.ToString());

            return response;
        }
    }
}
