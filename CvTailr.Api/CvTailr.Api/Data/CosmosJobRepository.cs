using CvTailr.Api.Configuration;
using CvTailr.Api.Data.Interfaces;
using CvTailr.Shared.Jobs;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace CvTailr.Api.Data;

public class CosmosJobRepository : IJobRepository
{
    private readonly Container _container;

    public CosmosJobRepository(CosmosClient cosmosClient, IOptions<CosmosOptions> options)
    {
        var cosmosOptions = options.Value;
        _container = cosmosClient.GetContainer(cosmosOptions.DatabaseName, cosmosOptions.JobContainerName);
    }

    public async Task<Job?> GetByIdAsync(string userId, string jobId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<Job>(jobId, new PartitionKey(userId), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<Job>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.userId = @userId ORDER BY c.updatedAt DESC")
            .WithParameter("@userId", userId);
        var requestOptions = new QueryRequestOptions { PartitionKey = new PartitionKey(userId) };

        var results = new List<Job>();
        using var iterator = _container.GetItemQueryIterator<Job>(query, requestOptions: requestOptions);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(page);
        }

        return results;
    }

    public Task UpsertAsync(Job job, CancellationToken cancellationToken = default) =>
        _container.UpsertItemAsync(job, new PartitionKey(job.UserId), cancellationToken: cancellationToken);
}
