using CvTailr.Api.Configuration;
using Microsoft.Azure.Cosmos;

namespace CvTailr.Api.Data;

/// <summary>
/// Creates the Cosmos containers this project needs on startup if they don't already exist. Never
/// migrates or deletes an existing container — a partition-key mismatch (e.g. a container that
/// predates a hierarchical-partition-key change) throws a clear error instead of silently working
/// around it, since this project does not auto-delete Cosmos containers/data.
/// </summary>
public static class CosmosContainerProvisioner
{
    public static async Task EnsureContainersAsync(Database database, CosmosOptions options)
    {
        await EnsureHierarchicalLedgerContainerAsync(database, options.ContainerName);
        await database.CreateContainerIfNotExistsAsync(options.CvContainerName, "/userId");
        await database.CreateContainerIfNotExistsAsync(options.JobContainerName, "/userId");
        await database.CreateContainerIfNotExistsAsync(options.TailoredCvContainerName, "/jobId");
    }

    // LedgerEntry moved to a hierarchical (/cvId, /jobId) partition key as of task 013 — hierarchical
    // partition keys can't be added to an existing container, so this only CREATES the container with
    // the hierarchical key when it doesn't exist yet. If it already exists with the old single
    // "/cvId" key, this throws with a clear message instead of silently leaving the mismatched
    // container in place or auto-deleting it — this project deliberately does not delete Cosmos
    // containers/data automatically. Since this is local dev data (nothing persisted here matters
    // yet), the fix is to delete the ledger container once yourself (Azure Portal / Data Explorer /
    // az cosmosdb sql container delete) and restart the app so it gets recreated hierarchically.
    private static async Task EnsureHierarchicalLedgerContainerAsync(Database database, string containerName)
    {
        var hierarchicalPaths = new List<string> { "/cvId", "/jobId" };
        var container = database.GetContainer(containerName);

        try
        {
            var existing = await container.ReadContainerAsync();
            if (existing.Resource.PartitionKeyPaths.SequenceEqual(hierarchicalPaths))
            {
                return;
            }

            throw new InvalidOperationException(
                $"Cosmos container '{containerName}' exists with partition key path(s) " +
                $"[{string.Join(", ", existing.Resource.PartitionKeyPaths)}], but task 013 requires the " +
                $"hierarchical key [{string.Join(", ", hierarchicalPaths)}]. Hierarchical partition keys can't be " +
                "added to an existing container. This is local dev data — delete the container manually " +
                "(Azure Portal / Data Explorer / az cosmosdb sql container delete) and restart the app so it gets " +
                "recreated with the hierarchical key.");
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Container doesn't exist yet — fall through to create it below.
        }

        await database.CreateContainerIfNotExistsAsync(new ContainerProperties(containerName, hierarchicalPaths));
    }
}
