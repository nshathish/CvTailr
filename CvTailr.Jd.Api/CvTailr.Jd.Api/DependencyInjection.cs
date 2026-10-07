using CvTailr.Jd.Api.Application;
using CvTailr.Jd.Api.Infrastructure;
using CvTailr.Jd.Api.Infrastructure.Foundry;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

namespace CvTailr.Jd.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddJdFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FoundryOptions>(configuration.GetSection(FoundryOptions.SectionName));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(configuration.GetSection("AzureAd"));

        services.AddAuthorization();

        services.AddScoped<IFoundryClient, FoundryClient>();
        services.AddScoped<IJdParsingService, JdParsingService>();
        services.AddScoped<IJdParseOrchestrator, JdParseOrchestrator>();

        // JdUrlFetcher is registered as a concrete type, not an interface: it needs a DI-managed
        // typed HttpClient, but it has exactly one caller (JdSourceResolver) and no second
        // implementation or test mock has ever been needed.
        services.AddHttpClient<JdUrlFetcher>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            // Redirects are followed manually in JdUrlFetcher so each hop can be re-validated.
            AllowAutoRedirect = false,
            // SSRF protection: resolve the host ourselves and only ever connect to a validated public IP.
            ConnectCallback = JdUrlFetcher.ConnectCallback
        });

        // JdHtmlExtractor is never registered at all — it's stateless with no dependencies DI needs
        // to supply, so JdSourceResolver just instantiates it directly.
        services.AddScoped<IJdSourceResolver, JdSourceResolver>();

        return services;
    }
}
