# CvTailr.Api

.NET 10 Minimal API. See [CLAUDE.md](CLAUDE.md) (this project) and the
[root CLAUDE.md](../../CLAUDE.md) for architecture and conventions.

## Local configuration (secrets)

`appsettings.json` only holds empty placeholders for `Foundry` and `EntraId` —
never commit real values there. Configure real values locally with
`dotnet user-secrets`, run from this project's directory
(`CvTailr.Api/CvTailr.Api`):

```
dotnet user-secrets set "Foundry:Endpoint" "https://<your-foundry-resource>.services.ai.azure.com"
dotnet user-secrets set "Foundry:ApiKey" "<your-foundry-key>"
dotnet user-secrets set "Foundry:JdParsingDeploymentName" "<deployment-name>"
dotnet user-secrets set "EntraId:Authority" "https://login.microsoftonline.com/<tenant-id>/v2.0"
dotnet user-secrets set "EntraId:Audience" "<api-app-registration-client-id>"
```

`Foundry:ApiKey` is optional — if it's left unset, `FoundryClient` falls back
to `DefaultAzureCredential` (Entra ID) to authenticate against the Foundry
endpoint instead of a key.

## Running

```
dotnet run
```

In Development, the OpenAPI document is served at `/openapi/v1.json`.
