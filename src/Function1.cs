using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace CfsAlerts
{
    public class Function1
    {
        private readonly DurableTaskClient _durableTaskClient;
        private readonly ILogger _logger;

        public Function1(ILoggerFactory loggerFactory, DurableTaskClient durableTaskClient)
        {
            _durableTaskClient = durableTaskClient;
            _logger = loggerFactory.CreateLogger<Function1>();
        }

        [Function("Function1")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            await _durableTaskClient.ScheduleNewOrchestrationInstanceAsync("MonitorJobStatus");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            await response.WriteStringAsync("Welcome to Azure Functions!");

            return response;
        }
    }
}
