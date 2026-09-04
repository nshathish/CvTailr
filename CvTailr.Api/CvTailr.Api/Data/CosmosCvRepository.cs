using CvTailr.Api.Configuration;
using CvTailr.Api.Data.Interfaces;
using CvTailr.Shared.Cv;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace CvTailr.Api.Data;

public class CosmosCvRepository : ICvRepository
{
    private readonly Container _container;

    public CosmosCvRepository(CosmosClient cosmosClient, IOptions<CosmosOptions> options)
    {
        var cosmosOptions = options.Value;
        _container = cosmosClient.GetContainer(cosmosOptions.DatabaseName, cosmosOptions.CvContainerName);
    }

    public async Task<CvDocument?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.userId = @userId")
            .WithParameter("@userId", userId);
        var requestOptions = new QueryRequestOptions { PartitionKey = new PartitionKey(userId) };

        using var iterator = _container.GetItemQueryIterator<CvDocument>(query, requestOptions: requestOptions);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            var match = page.FirstOrDefault();
            if (match is not null)
                return match;
        }

        return null;
    }

    public async Task UpsertAsync(CvDocument document, CancellationToken cancellationToken = default)
    {
        var existing = await GetByUserIdAsync(document.UserId, cancellationToken);
        if (existing is not null)
            document.Id = existing.Id;

        await _container.UpsertItemAsync(document, new PartitionKey(document.UserId), cancellationToken: cancellationToken);
    }
}
