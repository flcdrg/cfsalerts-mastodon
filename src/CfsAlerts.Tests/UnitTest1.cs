using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Net;
using System.Reflection;

namespace CfsAlerts.Tests;

[UsesVerify]
public class CfsFunctionTests
{

    public static IHttpClientFactory GetStringClient(string returnValue, HttpStatusCode returnStatusCode = HttpStatusCode.OK)
    {
        var httpMessageHandler = Substitute.For<HttpMessageHandler>();
        var httpClientFactory = Substitute.For<IHttpClientFactory>();

        httpMessageHandler
            .GetType()
            .GetMethod("SendAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(httpMessageHandler, new object[] { Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>() })
            .Returns(Task.FromResult(new HttpResponseMessage(returnStatusCode) { Content = new StringContent(returnValue) }));

        HttpClient httpClient = new(httpMessageHandler);
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        return httpClientFactory;
    }

    [Fact]
    public async Task Test1()
    {
        string resourceName = "CfsAlerts.Tests.oldList.xml";
        var assembly = Assembly.GetExecutingAssembly();

        await using Stream stream = assembly.GetManifestResourceStream(resourceName);
        using StreamReader reader = new StreamReader(stream);
        string oldListContent = reader.ReadToEnd();

        var httpClientFactory = GetStringClient(oldListContent);
        var function = new CfsFunction(httpClientFactory, new NullLogger<CfsFunction>());

        var oldList = new List<CfsFeedItem>();
        var newList = await function.CheckAlerts(oldList);

        await Verify(newList);
    }
}