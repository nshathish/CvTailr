using CvTailr.Api;
using CvTailr.Api.Common.Cosmos;
using CvTailr.Api.Features.Cv;
using CvTailr.Api.Features.Drill;
using CvTailr.Api.Features.Jd;
using CvTailr.Api.Features.Jobs;
using CvTailr.Api.Features.Ledger;
using CvTailr.Api.Features.Scoring;
using CvTailr.Api.Features.Tailoring;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddCommonInfrastructure(builder.Configuration);
builder.Services.AddCvFeature();
builder.Services.AddJdFeature();
builder.Services.AddJobsFeature();
builder.Services.AddScoringFeature();
builder.Services.AddTailoringFeature();
builder.Services.AddLedgerFeature();
builder.Services.AddDrillFeature();

var app = builder.Build();

using (var startupScope = app.Services.CreateScope())
{
    var cosmosClient = startupScope.ServiceProvider.GetRequiredService<CosmosClient>();
    var cosmosOptions = startupScope.ServiceProvider.GetRequiredService<IOptions<CosmosOptions>>().Value;

    var database = await cosmosClient.CreateDatabaseIfNotExistsAsync(cosmosOptions.DatabaseName);
    await CosmosContainerProvisioner.EnsureContainersAsync(database.Database, cosmosOptions);
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
app.MapJobEndpoints();
app.MapJobListingEndpoints();

app.Run();
