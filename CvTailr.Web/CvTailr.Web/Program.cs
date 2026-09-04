using CvTailr.Web.Components;
using CvTailr.Web.Configuration;
using CvTailr.Web.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.TokenCacheProviders.Distributed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDistributedMemoryCache();

builder.Services.Configure<MsalDistributedTokenCacheAdapterOptions>(options => { options.Encrypt = true; });

builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection(ConfigurationSections.AzureAd))
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddDownstreamApi(
        ConfigurationSections.DownstreamApi,
        builder.Configuration.GetSection(ConfigurationSections.DownstreamApi))
    .AddDistributedTokenCaches();

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAntiforgery();

builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<WorkflowStateService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Error",
        createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute(
    "/not-found",
    createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapGroup("/authentication")
    .MapLoginAndLogout();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();