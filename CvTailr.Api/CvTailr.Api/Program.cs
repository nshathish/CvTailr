using CvTailr.Api.Clients;
using CvTailr.Api.Configuration;
using CvTailr.Api.Endpoints;
using CvTailr.Api.Services;
using CvTailr.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.Configure<FoundryOptions>(builder.Configuration.GetSection(FoundryOptions.SectionName));
builder.Services.Configure<EntraIdOptions>(builder.Configuration.GetSection(EntraIdOptions.SectionName));

var entraId = builder.Configuration.GetSection(EntraIdOptions.SectionName).Get<EntraIdOptions>() ?? new EntraIdOptions();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = entraId.Authority;
        options.Audience = entraId.Audience;
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<IFoundryClient, FoundryClient>();
builder.Services.AddScoped<IJdParsingService, JdParsingService>();
builder.Services.AddScoped<ICvParsingService, CvParsingService>();
builder.Services.AddScoped<IScoringService, ScoringService>();

var app = builder.Build();

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

app.Run();
