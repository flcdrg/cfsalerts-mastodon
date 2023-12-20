using CfsAlerts;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration(builder =>
    {
        builder.AddUserSecrets<Program>();
    })
    .ConfigureServices(services =>
    {
        services.AddHttpClient();
        services.AddDurableTaskClient(builder =>
        {
            builder.UseGrpc();
        });

        services.AddOptions<MastodonSettings>()
            .Configure<IConfiguration>((settings, configuration) =>
            {
                configuration.GetSection(nameof(MastodonSettings)).Bind(settings);
            });
    })
    .Build();

host.Run();