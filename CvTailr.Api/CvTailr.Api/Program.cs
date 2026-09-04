using Azure.Identity;
using CvTailr.Api.Clients;
using CvTailr.Api.Configuration;
using CvTailr.Api.Data;
using CvTailr.Api.Data.Interfaces;
using CvTailr.Api.Endpoints;
using CvTailr.Api.Services;
using CvTailr.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();

// Minimal API request/response binding uses its own JsonSerializerOptions instance — without
// this, enum request bodies (e.g. { "outcome": "Fail" }) fail to bind since the default expects
// numeric enum values.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.Configure<FoundryOptions>(builder.Configuration.GetSection(FoundryOptions.SectionName));
builder.Services.Configure<EntraIdOptions>(builder.Configuration.GetSection(EntraIdOptions.SectionName));
builder.Services.Configure<CosmosOptions>(builder.Configuration.GetSection(CosmosOptions.SectionName));

var entraId = builder.Configuration.GetSection(EntraIdOptions.SectionName).Get<EntraIdOptions>() ?? new EntraIdOptions();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = entraId.Authority;
        // External ID tokens are sometimes stamped with the bare ClientId as `aud` rather than
        // the "api://{clientId}" Application ID URI, depending on how the API's Application ID
        // URI is configured — accept either form.
        options.TokenValidationParameters.ValidAudiences = [entraId.Audience, entraId.ClientId];
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<IFoundryClient, FoundryClient>();
builder.Services.AddScoped<IJdParsingService, JdParsingService>();
builder.Services.AddScoped<ICvParsingService, CvParsingService>();
builder.Services.AddScoped<IScoringService, ScoringService>();
builder.Services.AddScoped<ITailoringService, TailoringService>();
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();

builder.Services.AddSingleton(sp =>
{
    var cosmosOptions = sp.GetRequiredService<IOptions<CosmosOptions>>().Value;
    var clientOptions = new CosmosClientOptions
    {
        // Gateway (HTTP) mode rather than Direct (TCP): works everywhere, including behind
        // restrictive networks/firewalls and against lightweight emulators that don't expose the
        // Direct-mode TCP port range.
        ConnectionMode = ConnectionMode.Gateway,
        SerializerOptions = new CosmosSerializationOptions
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        }
    };

    return string.IsNullOrWhiteSpace(cosmosOptions.AccountKey)
        ? new CosmosClient(cosmosOptions.Endpoint, new DefaultAzureCredential(), clientOptions)
        : new CosmosClient(cosmosOptions.Endpoint, cosmosOptions.AccountKey, clientOptions);
});
builder.Services.AddScoped<ILedgerRepository, CosmosLedgerRepository>();
builder.Services.AddScoped<ILedgerService, LedgerService>();
builder.Services.AddScoped<IDrillService, DrillService>();
builder.Services.AddScoped<ICvRepository, CosmosCvRepository>();

var app = builder.Build();

using (var startupScope = app.Services.CreateScope())
{
    var cosmosClient = startupScope.ServiceProvider.GetRequiredService<CosmosClient>();
    var cosmosOptions = startupScope.ServiceProvider.GetRequiredService<IOptions<CosmosOptions>>().Value;

    var database = await cosmosClient.CreateDatabaseIfNotExistsAsync(cosmosOptions.DatabaseName);
    await database.Database.CreateContainerIfNotExistsAsync(cosmosOptions.ContainerName, "/cvId");
    await database.Database.CreateContainerIfNotExistsAsync(cosmosOptions.CvContainerName, "/userId");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.DarkMode = false;
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .AllowAnonymous();

app.MapJdEndpoints();
app.MapCvEndpoints();
app.MapScoringEndpoints();
app.MapTailoringEndpoints();
app.MapLedgerEndpoints();
app.MapDrillEndpoints();

app.Run();
