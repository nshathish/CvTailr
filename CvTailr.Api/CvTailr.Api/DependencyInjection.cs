using Azure.Identity;
using CvTailr.Api.Common.Auth;
using CvTailr.Api.Common.Cosmos;
using CvTailr.Api.Common.Foundry;
using CvTailr.Api.Common.SkillTagging;
using CvTailr.Api.Common.TailoredCv;
using CvTailr.Api.Features.Cv;
using CvTailr.Api.Features.Drill;
using CvTailr.Api.Features.Jobs;
using CvTailr.Api.Features.Jobs.Listings;
using CvTailr.Api.Features.Ledger;
using CvTailr.Api.Features.Scoring;
using CvTailr.Api.Features.Tailoring;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CvTailr.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddCommonInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FoundryOptions>(configuration.GetSection(FoundryOptions.SectionName));
        services.Configure<EntraIdOptions>(configuration.GetSection(EntraIdOptions.SectionName));
        services.Configure<CosmosOptions>(configuration.GetSection(CosmosOptions.SectionName));

        var entraId = configuration.GetSection(EntraIdOptions.SectionName).Get<EntraIdOptions>() ?? new EntraIdOptions();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = entraId.Authority;
                // External ID tokens are sometimes stamped with the bare ClientId as `aud` rather than
                // the "api://{clientId}" Application ID URI, depending on how the API's Application ID
                // URI is configured — accept either form.
                options.TokenValidationParameters.ValidAudiences = [entraId.Audience, entraId.ClientId];
            });

        services.AddAuthorization();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();

        services.AddScoped<IFoundryClient, FoundryClient>();
        services.AddScoped<ISkillTaggingService, SkillTaggingService>();
        services.AddScoped<ITailoredCvRepository, CosmosTailoredCvRepository>();

        services.AddSingleton(sp =>
        {
            var cosmosOptions = sp.GetRequiredService<IOptions<CosmosOptions>>().Value;
            var clientOptions = new CosmosClientOptions
            {
                // Gateway (HTTP) mode rather than Direct (TCP): works everywhere, including behind
                // restrictive networks/firewalls and against lightweight emulators that don't expose the
                // Direct-mode TCP port range.
                ConnectionMode = ConnectionMode.Gateway,
                // System.Text.Json (matching the API's own JSON config) rather than the plain
                // CosmosSerializationOptions path, so every enum — JobStatus included — is stored/read as a
                // string instead of Cosmos's default int encoding.
                UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    Converters = { new JsonStringEnumConverter() }
                }
            };

            return string.IsNullOrWhiteSpace(cosmosOptions.AccountKey)
                ? new CosmosClient(cosmosOptions.Endpoint, new DefaultAzureCredential(), clientOptions)
                : new CosmosClient(cosmosOptions.Endpoint, cosmosOptions.AccountKey, clientOptions);
        });

        return services;
    }

    public static IServiceCollection AddCvFeature(this IServiceCollection services)
    {
        services.AddScoped<ICvParsingService, CvParsingService>();
        services.AddScoped<ICvSourceExtractor, CvSourceExtractor>();
        services.AddScoped<ICvRepository, CosmosCvRepository>();

        return services;
    }

    public static IServiceCollection AddJobsFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IJobRepository, CosmosJobRepository>();
        services.AddScoped<IJobService, JobService>();

        services.AddScoped<IJobListingRepository, CosmosJobListingRepository>();
        services.AddScoped<IJobListingCaptureService, JobListingCaptureService>();
        services.AddScoped<IJobListingsService, JobListingsService>();

        // Hidden internal service called only by JobEndpoints' POST /api/jobs/parse — see
        // JdServiceClient. Jd.Api is never called by Web/Mobile directly.
        services.Configure<JdApiOptions>(configuration.GetSection(JdApiOptions.SectionName));
        services.AddHttpClient<IJdServiceClient, JdServiceClient>((sp, client) =>
        {
            var jdApiOptions = sp.GetRequiredService<IOptions<JdApiOptions>>().Value;
            client.BaseAddress = new Uri(jdApiOptions.BaseUrl);
        });

        return services;
    }

    public static IServiceCollection AddScoringFeature(this IServiceCollection services)
    {
        services.AddScoped<IScoringService, ScoringService>();

        return services;
    }

    public static IServiceCollection AddTailoringFeature(this IServiceCollection services)
    {
        services.AddScoped<ITailoringService, TailoringService>();

        return services;
    }

    public static IServiceCollection AddLedgerFeature(this IServiceCollection services)
    {
        services.AddScoped<ILedgerRepository, CosmosLedgerRepository>();
        services.AddScoped<ILedgerService, LedgerService>();

        return services;
    }

    public static IServiceCollection AddDrillFeature(this IServiceCollection services)
    {
        services.AddScoped<IDrillService, DrillService>();

        return services;
    }
}
