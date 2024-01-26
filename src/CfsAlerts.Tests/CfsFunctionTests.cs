using System.Net;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CfsAlerts.Tests;

public class CfsFunctionTests
{
    public static IHttpClientFactory GetStringClient(string returnValue,
        HttpStatusCode returnStatusCode = HttpStatusCode.OK)
    {
        var httpMessageHandler = Substitute.For<HttpMessageHandler>()!;
        var httpClientFactory = Substitute.For<IHttpClientFactory>()!;

        httpMessageHandler
            .GetType()
            .GetMethod("SendAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(httpMessageHandler, new object[] { Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>() })!
            .Returns(Task.FromResult(new HttpResponseMessage(returnStatusCode)
                { Content = new StringContent(returnValue) }));

        HttpClient httpClient = new(httpMessageHandler);
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        return httpClientFactory;
    }

    [Fact]
    public async Task Test1()
    {
        var assembly = Assembly.GetExecutingAssembly();

        string oldListContent;
        string newListContent;

        await using (var stream = assembly.GetManifestResourceStream("CfsAlerts.Tests.oldList.xml")!)
        {
            using var reader = new StreamReader(stream);
            oldListContent = reader.ReadToEnd();
        }

        await using (var stream = assembly.GetManifestResourceStream("CfsAlerts.Tests.newList.xml")!)
        {
            using var reader = new StreamReader(stream);
            newListContent = reader.ReadToEnd();
        }

        var settingOptions = Options.Create<MastodonSettings>(new MastodonSettings());
        var httpClientFactory = GetStringClient(newListContent);
        var function = new CfsFunction(httpClientFactory, new NullLogger<CfsFunction>(), settingOptions);

        var oldList = new List<CfsFeedItem>();
        var newList = await function.CheckAlerts(oldList);

        await Verify(newList);
    }
}