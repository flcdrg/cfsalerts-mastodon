using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CfsAlerts.Tests;

public class StartOrchestrationTests
{
    private const string InstanceId = "MonitorJobStatus 1B5BF432-5DEA-481D-AE45-32AB347C8F2F";

    [Fact]
    public async Task Run_WhenInstanceIsMissing_SchedulesWithActiveStatusDeduplication()
    {
        var client = Substitute.For<DurableTaskClient>("test");
        client.GetInstanceAsync(InstanceId, false, Arg.Any<CancellationToken>())
            .Returns((OrchestrationMetadata?)null);
        StartOrchestrationOptions? scheduledOptions = null;
        client.ScheduleNewOrchestrationInstanceAsync(
                Arg.Any<TaskName>(), Arg.Any<object>(),
                Arg.Do<StartOrchestrationOptions>(options => scheduledOptions = options),
                Arg.Any<CancellationToken>())
            .Returns(InstanceId);
        var request = CreateRequest();
        var function = new StartOrchestration(NullLogger<StartOrchestration>.Instance);

        var response = await function.Run(request, client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await client.Received(1).ScheduleNewOrchestrationInstanceAsync(
            Arg.Is<TaskName>(name => name.Name == nameof(MonitorJobStatus)),
            Arg.Is<List<CfsFeedItem>>(items => items.Count == 0),
            Arg.Any<StartOrchestrationOptions>(), Arg.Any<CancellationToken>());
        Assert.NotNull(scheduledOptions);
        Assert.Equal(InstanceId, scheduledOptions.InstanceId);
        Assert.Contains("Running", scheduledOptions.DedupeStatuses!);
        Assert.Contains("Pending", scheduledOptions.DedupeStatuses!);
        Assert.Contains("ContinuedAsNew", scheduledOptions.DedupeStatuses!);
        Assert.Contains("Suspended", scheduledOptions.DedupeStatuses!);
    }

    [Theory]
    [InlineData(OrchestrationRuntimeStatus.Running)]
    [InlineData(OrchestrationRuntimeStatus.Pending)]
#pragma warning disable CS0618
    [InlineData(OrchestrationRuntimeStatus.ContinuedAsNew)]
#pragma warning restore CS0618
    [InlineData(OrchestrationRuntimeStatus.Suspended)]
    public async Task Run_WhenInstanceIsActive_DoesNotReplaceIt(OrchestrationRuntimeStatus status)
    {
        var client = Substitute.For<DurableTaskClient>("test");
        client.GetInstanceAsync(InstanceId, false, Arg.Any<CancellationToken>())
            .Returns(new OrchestrationMetadata(nameof(MonitorJobStatus), InstanceId) { RuntimeStatus = status });
        var function = new StartOrchestration(NullLogger<StartOrchestration>.Instance);

        var response = await function.Run(CreateRequest(), client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await client.DidNotReceive().ScheduleNewOrchestrationInstanceAsync(
            Arg.Any<TaskName>(), Arg.Any<object>(), Arg.Any<StartOrchestrationOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_WhenInstanceHasFailed_SchedulesReplacement()
    {
        var client = Substitute.For<DurableTaskClient>("test");
        client.GetInstanceAsync(InstanceId, false, Arg.Any<CancellationToken>())
            .Returns(new OrchestrationMetadata(nameof(MonitorJobStatus), InstanceId) { RuntimeStatus = OrchestrationRuntimeStatus.Failed });
        client.ScheduleNewOrchestrationInstanceAsync(
                Arg.Any<TaskName>(), Arg.Any<object>(), Arg.Any<StartOrchestrationOptions>(), Arg.Any<CancellationToken>())
            .Returns(InstanceId);
        var function = new StartOrchestration(NullLogger<StartOrchestration>.Instance);

        await function.Run(CreateRequest(), client);

        await client.Received(1).ScheduleNewOrchestrationInstanceAsync(
            Arg.Any<TaskName>(), Arg.Any<object>(), Arg.Any<StartOrchestrationOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_WhenClientFails_PropagatesFailure()
    {
        var client = Substitute.For<DurableTaskClient>("test");
        client.GetInstanceAsync(InstanceId, false, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<OrchestrationMetadata?>(new InvalidOperationException("unavailable")));
        var function = new StartOrchestration(NullLogger<StartOrchestration>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => function.Run(CreateRequest(), client));
    }

    [Fact]
    public async Task Run_WhenSchedulingFails_PropagatesFailure()
    {
        var client = Substitute.For<DurableTaskClient>("test");
        client.GetInstanceAsync(InstanceId, false, Arg.Any<CancellationToken>())
            .Returns((OrchestrationMetadata?)null);
        client.ScheduleNewOrchestrationInstanceAsync(
                Arg.Any<TaskName>(), Arg.Any<object>(), Arg.Any<StartOrchestrationOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new InvalidOperationException("unavailable")));
        var function = new StartOrchestration(NullLogger<StartOrchestration>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => function.Run(CreateRequest(), client));
    }

    private static HttpRequestData CreateRequest()
    {
        var context = Substitute.For<FunctionContext>();
        var request = Substitute.For<HttpRequestData>(context);
        var response = Substitute.For<HttpResponseData>(context);
        response.Headers.Returns(new HttpHeadersCollection());
        response.Body.Returns(new MemoryStream());
        request.CreateResponse().Returns(response);
        return request;
    }
}
