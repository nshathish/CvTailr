using CvTailr.Shared.Cv;
using CvTailr.Shared.Jobs;
using CvTailr.Shared.Tailoring;
using CvTailr.Web.Services.Interfaces;
using Microsoft.Identity.Abstractions;

namespace CvTailr.Web.Services;

public class TailorApiClient(IDownstreamApi downstreamApi) : ApiClientBase(downstreamApi), ITailorApiClient
{
    public async Task<TailoringProposal> ProposeTailoringAsync(string jobId, CancellationToken ct = default)
    {
        const string relativePath = "api/tailor/propose";
        using var response = await DownstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "POST";
                options.RelativePath = relativePath;
            },
            content: JsonContent.Create(new { jobId }, options: JsonOptions),
            cancellationToken: ct);

        await ThrowIfUnsuccessfulAsync(response, "POST", relativePath, ct);

        var result = await response.Content.ReadFromJsonAsync<TailoringProposal>(JsonOptions, ct);
        return result ?? throw new ApiClientException($"POST /{relativePath} returned an empty response body.");
    }

    // Mirrors the Api's TailorApplyRequest shape exactly: ApprovedBulletRewriteIds says which
    // rewrites to apply, while ProposedBulletRewrites carries the full proposal objects the Api
    // needs to look up by that id — sending only the approved subset for both is sufficient (the
    // Api intersects by id) and keeps the request smaller. New bullets/skills carry no separate
    // id list since the client only ever sends the ones it's approving.
    public async Task<TailorApplyResult> ApplyTailoringAsync(
        string jobId,
        List<string> approvedBulletRewriteIds,
        List<BulletRewriteProposal> approvedRewrites,
        List<NewBulletProposal> approvedNewBullets,
        List<NewSkillProposal> approvedNewSkills,
        CancellationToken ct = default)
    {
        const string relativePath = "api/tailor/apply";
        using var response = await DownstreamApi.CallApiForUserAsync(
            ServiceName,
            options =>
            {
                options.HttpMethod = "POST";
                options.RelativePath = relativePath;
            },
            content: JsonContent.Create(
                new
                {
                    jobId,
                    approvedBulletRewriteIds,
                    proposedBulletRewrites = approvedRewrites,
                    approvedNewBullets,
                    approvedNewSkills
                },
                options: JsonOptions),
            cancellationToken: ct);

        await ThrowIfUnsuccessfulAsync(response, "POST", relativePath, ct);

        var result = await response.Content.ReadFromJsonAsync<TailorApplyResult>(JsonOptions, ct);
        return result ?? throw new ApiClientException($"POST /{relativePath} returned an empty response body.");
    }
}

public record TailorApplyResult(CvDocument CvDocument, Job Job, List<string> Warnings);
