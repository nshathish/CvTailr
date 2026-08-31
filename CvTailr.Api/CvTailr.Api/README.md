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

Ledger persistence needs an Azure Cosmos DB account:

```
dotnet user-secrets set "Cosmos:Endpoint" "https://<your-cosmos-account>.documents.azure.com:443/"
dotnet user-secrets set "Cosmos:AccountKey" "<your-cosmos-account-key>"
dotnet user-secrets set "Cosmos:DatabaseName" "cvtailr"
dotnet user-secrets set "Cosmos:ContainerName" "ledgerEntries"
```

`Cosmos:AccountKey` is optional — if left unset, `CosmosClient` falls back to
`DefaultAzureCredential` instead of a key. The database and container are
created automatically on startup if they don't already exist, partitioned by
`/cvId`. `CosmosClientOptions.ConnectionMode` is set to `Gateway` (HTTP) rather
than the default `Direct` (TCP) mode, since Direct mode fails against
lightweight/local emulators and restrictive networks.

For local development without a real Azure Cosmos account, run the emulator
in Docker and point `Cosmos:Endpoint` at it with the well-known emulator key:

```
docker run --detach --publish 8081:8081 --publish 1234:1234 --name cvtailr-cosmos-emulator mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview
```

```
dotnet user-secrets set "Cosmos:Endpoint" "http://localhost:8081"
dotnet user-secrets set "Cosmos:AccountKey" "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
dotnet user-secrets set "Cosmos:DatabaseName" "cvtailr"
dotnet user-secrets set "Cosmos:ContainerName" "ledgerEntries"
```

The key above is Microsoft's publicly documented, well-known Cosmos DB
Emulator key — it only works against a local emulator instance, never a real
Azure account, so it's fine to keep in plain text.

## Running

```
dotnet run
```

In Development, the OpenAPI document is served at `/openapi/v1.json`.
