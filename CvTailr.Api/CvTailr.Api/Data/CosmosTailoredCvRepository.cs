using CvTailr.Api.Configuration;
using CvTailr.Api.Data.Interfaces;
using CvTailr.Shared.Cv;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace CvTailr.Api.Data;

public class CosmosTailoredCvRepository : ITailoredCvRepository
{
    private readonly Container _container;

    public CosmosTailoredCvRepository(CosmosClient cosmosClient, IOptions<CosmosOptions> options)
    {
        var cosmosOptions = options.Value;
        _container = cosmosClient.GetContainer(cosmosOptions.DatabaseName, cosmosOptions.TailoredCvContainerName);
    }

    public async Task<TailoredCvDocument?> GetByJobIdAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.jobId = @jobId")
            .WithParameter("@jobId", jobId);
        var requestOptions = new QueryRequestOptions { PartitionKey = new PartitionKey(jobId) };

        using var iterator = _container.GetItemQueryIterator<TailoredCvDocument>(query, requestOptions: requestOptions);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            var match = page.FirstOrDefault();
            if (match is not null)
                return match;
        }

        return null;
    }

    public async Task<List<string>> GetJobIdsByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Cross-partition (container is partitioned by /jobId, not /userId) — deliberate, since this
        // exists specifically to avoid a per-job lookup: one query here replaces what would otherwise
        // be one GetByJobIdAsync call per Job in the caller's list.
        var query = new QueryDefinition("SELECT VALUE c.jobId FROM c WHERE c.userId = @userId")
            .WithParameter("@userId", userId);

        var results = new List<string>();
        using var iterator = _container.GetItemQueryIterator<string>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(page);
        }

        return results;
    }

    public async Task UpsertAsync(TailoredCvDocument document, CancellationToken cancellationToken = default)
    {
        var existing = await GetByJobIdAsync(document.JobId, cancellationToken);
        if (existing is not null)
            document.Id = existing.Id;

        await _container.UpsertItemAsync(document, new PartitionKey(document.JobId), cancellationToken: cancellationToken);
    }

    public async Task DeleteByJobIdAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var existing = await GetByJobIdAsync(jobId, cancellationToken);
        if (existing is null)
            return;

        await _container.DeleteItemAsync<TailoredCvDocument>(existing.Id, new PartitionKey(jobId), cancellationToken: cancellationToken);
    }
}
