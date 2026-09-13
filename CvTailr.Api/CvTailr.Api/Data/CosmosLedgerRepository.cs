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
        // Cross-partition query by id — kept deliberately, even under the hierarchical (cvId,
        // jobId) partition key: the {entryId}-only routes (drill-attempt/review/confirm-status)
        // don't carry cvId/jobId, and those routes stay unchanged in shape per task 013. AddAsync/
        // UpdateAsync below still use the full hierarchical key once an entry (with its own
        // CvId/JobId) is in hand, so writes remain properly partitioned.
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

        // Partial hierarchical partition key (cvId only, no jobId) — a "prefix" partition query
        // scoped to every job under this cvId, rather than a full cross-partition fan-out.
        var partitionKey = new PartitionKeyBuilder().Add(cvId).Build();
        var requestOptions = new QueryRequestOptions { PartitionKey = partitionKey };

        var results = new List<LedgerEntry>();
        using var iterator = _container.GetItemQueryIterator<LedgerEntry>(query, requestOptions: requestOptions);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(page);
        }

        return results;
    }

    public async Task<List<LedgerEntry>> GetByCvAndJobIdAsync(string cvId, string jobId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.cvId = @cvId AND c.jobId = @jobId")
            .WithParameter("@cvId", cvId)
            .WithParameter("@jobId", jobId);

        var partitionKey = new PartitionKeyBuilder().Add(cvId).Add(jobId).Build();
        var requestOptions = new QueryRequestOptions { PartitionKey = partitionKey };

        var results = new List<LedgerEntry>();
        using var iterator = _container.GetItemQueryIterator<LedgerEntry>(query, requestOptions: requestOptions);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(page);
        }

        return results;
    }

    public Task AddAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
    {
        var partitionKey = new PartitionKeyBuilder().Add(entry.CvId).Add(entry.JobId).Build();
        return _container.CreateItemAsync(entry, partitionKey, cancellationToken: cancellationToken);
    }

    public Task UpdateAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
    {
        var partitionKey = new PartitionKeyBuilder().Add(entry.CvId).Add(entry.JobId).Build();
        return _container.UpsertItemAsync(entry, partitionKey, cancellationToken: cancellationToken);
    }
}
