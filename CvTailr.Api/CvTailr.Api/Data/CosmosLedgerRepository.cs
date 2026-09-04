using CvTailr.Api.Configuration;
using CvTailr.Api.Data.Interfaces;
using CvTailr.Shared.Ledger;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace CvTailr.Api.Data;

public class CosmosLedgerRepository : ILedgerRepository
{
    private readonly Container _container;

    public CosmosLedgerRepository(CosmosClient cosmosClient, IOptions<CosmosOptions> options)
    {
        var cosmosOptions = options.Value;
        _container = cosmosClient.GetContainer(cosmosOptions.DatabaseName, cosmosOptions.ContainerName);
    }

    public async Task<LedgerEntry?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        // CvId (the partition key) isn't known here, so this is a cross-partition query rather
        // than a point read — acceptable for this task's scale, revisit if this ever gets hot.
        var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
            .WithParameter("@id", id);

        using var iterator = _container.GetItemQueryIterator<LedgerEntry>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            var match = page.FirstOrDefault();
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    public async Task<List<LedgerEntry>> GetByCvIdAsync(string cvId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.cvId = @cvId")
            .WithParameter("@cvId", cvId);
        var requestOptions = new QueryRequestOptions { PartitionKey = new PartitionKey(cvId) };

        var results = new List<LedgerEntry>();
        using var iterator = _container.GetItemQueryIterator<LedgerEntry>(query, requestOptions: requestOptions);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(page);
        }

        return results;
    }

    public Task AddAsync(LedgerEntry entry, CancellationToken cancellationToken = default) =>
        _container.CreateItemAsync(entry, new PartitionKey(entry.CvId), cancellationToken: cancellationToken);

    public Task UpdateAsync(LedgerEntry entry, CancellationToken cancellationToken = default) =>
        _container.UpsertItemAsync(entry, new PartitionKey(entry.CvId), cancellationToken: cancellationToken);
}
