using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CfsAlerts;

public class CheckFeed
{
    private readonly ILogger _logger;

    public CheckFeed(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<CheckFeed>();
    }

    [Function("CheckFeed")]
    public void Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer)
    {
        _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            
        if (myTimer.ScheduleStatus is not null)
        {
            _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
        }
    }
}