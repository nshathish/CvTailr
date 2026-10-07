using CvTailr.Api.Common.Cosmos;
using CvTailr.Shared.Jobs;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace CvTailr.Api.Features.Jobs.Listings;

public class CosmosJobListingRepository : IJobListingRepository
{
    private const int MaxLastSeenAgeDays = 60;
    private const int MaxQueryItems = 500;

    private readonly Container _container;

    public CosmosJobListingRepository(CosmosClient cosmosClient, IOptions<CosmosOptions> options)
    {
        var cosmosOptions = options.Value;
        _container = cosmosClient.GetContainer(cosmosOptions.DatabaseName, cosmosOptions.JobListingContainerName);
    }

    public async Task<JobListing?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<JobListing>(id, new PartitionKey(id), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task UpsertAsync(JobListing listing, CancellationToken cancellationToken = default) =>
        _container.UpsertItemAsync(listing, new PartitionKey(listing.Id), cancellationToken: cancellationToken);

    public async Task<List<JobListing>> QueryActiveMatchesAsync(List<string> tags, CancellationToken cancellationToken = default)
    {
        if (tags.Count == 0)
            return [];

        var now = DateTimeOffset.UtcNow;
        var minLastSeenAt = now.AddDays(-MaxLastSeenAgeDays);

        // c.skillTags/c.validThrough/c.lastSeenAt/c.status are the camelCase JSON property names
        // System.Text.Json (JsonSerializerDefaults.Web, configured on the CosmosClient) actually
        // writes for JobListing's SkillTags/ValidThrough/LastSeenAt/Status. Dates are compared as
        // ISO 8601 strings (Cosmos has no native DateTime type) — "O" matches System.Text.Json's
        // own DateTimeOffset format, so lexicographic and chronological order agree.
        var query = new QueryDefinition(
                $"""
                 SELECT TOP {MaxQueryItems} * FROM c
                 WHERE c.status = @status
                   AND EXISTS(SELECT VALUE tag FROM tag IN c.skillTags WHERE ARRAY_CONTAINS(@tags, tag))
                   AND (NOT IS_DEFINED(c.validThrough) OR IS_NULL(c.validThrough) OR c.validThrough >= @now)
                   AND c.lastSeenAt >= @minLastSeenAt
                 """)
            .WithParameter("@status", JobListingStatus.Active.ToString())
            .WithParameter("@tags", tags)
            .WithParameter("@now", now.ToString("O"))
            .WithParameter("@minLastSeenAt", minLastSeenAt.ToString("O"));

        var results = new List<JobListing>();
        using var iterator = _container.GetItemQueryIterator<JobListing>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(page);
        }

        return results;
    }
}
