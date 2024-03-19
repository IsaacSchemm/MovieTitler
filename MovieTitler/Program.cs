using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MovieTitler;
using MovieTitler.Data;
using MovieTitler.HighLevel;
using MovieTitler.HighLevel.Feed;
using MovieTitler.HighLevel.Remote;
using MovieTitler.HighLevel.Signatures;
using MovieTitler.Interfaces;
using MovieTitler.LowLevel;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        if (Environment.GetEnvironmentVariable("CosmosDBAccountKey") is string accountKey)
            services.AddDbContext<BotDbContext>(options => options.UseCosmos(
                Environment.GetEnvironmentVariable("CosmosDBAccountEndpoint"),
                accountKey,
                databaseName: "MovieTitler"));
        else
            services.AddDbContext<BotDbContext>(options => options.UseCosmos(
                Environment.GetEnvironmentVariable("CosmosDBAccountEndpoint"),
                new DefaultAzureCredential(),
                databaseName: "MovieTitler"));

        services.AddSingleton<IApplicationInformation>(new AppInfo(
            ApplicationName: "MovieTitler",
            VersionNumber: "1.0",
            ApplicationHostname: Environment.GetEnvironmentVariable("ApplicationHost"),
            WebsiteUrl: $"https://github.com/IsaacSchemm/MovieTitler"));

        services.AddSingleton<IActorKeyProvider>(
            new KeyProvider(
                $"https://{Environment.GetEnvironmentVariable("KeyVaultHost")}"));

        services.AddHttpClient();

        services.AddScoped<ActivityPubTranslator>();
        services.AddScoped<ContentNegotiator>();
        services.AddScoped<FeedBuilder>();
        services.AddScoped<IdMapper>();
        services.AddScoped<InboxHandler>();
        services.AddScoped<MarkdownTranslator>();
        services.AddScoped<MastodonVerifier>();
        services.AddScoped<OutboundActivityProcessor>();
        services.AddScoped<RemoteInboxLocator>();
        services.AddScoped<Requester>();
    })
    .Build();

host.Run();

record AppInfo(
    string ApplicationName,
    string VersionNumber,
    string ApplicationHostname,
    string WebsiteUrl) : IApplicationInformation
{
    string IApplicationInformation.UserAgent =>
        $"{ApplicationName}/{VersionNumber} ({WebsiteUrl})";
}
