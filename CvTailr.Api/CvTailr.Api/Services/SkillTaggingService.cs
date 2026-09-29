using System.Text.Json;
using CvTailr.Api.Clients;
using CvTailr.Api.Configuration;
using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Cv;
using CvTailr.Shared.Jd;
using Microsoft.Extensions.Options;

namespace CvTailr.Api.Services;

public class SkillTaggingService(
    IFoundryClient foundryClient,
    IOptions<FoundryOptions> foundryOptions) : ISkillTaggingService
{
    private const int MaxCvTags = 60;

    private static readonly JsonSerializerOptions InputJsonOptions = new(JsonSerializerDefaults.Web);

    private const string RequirementTaggingSystemPrompt =
        """
        You are an expert technical recruiter tagging job requirements with skill/technology/practice
        tags for matching purposes.

        You will be given a JSON array of job requirements, each with "Skill", "Priority",
        "YearsRequired", and "Notes".

        Return a JSON object matching exactly this shape:
        {
          "Tags": [ [string, ...], ... ]
        }

        Rules:
        - "Tags" must have exactly one entry per input requirement, in the same order.
        - Each entry has 1 to 3 tags.
        - Each tag is a short (1 to 4 word) common name for a specific technology, practice, or
          skill (e.g. "kubernetes", "event-driven architecture", "mentoring") — use the most widely
          used full name rather than an abbreviation (e.g. "kubernetes" not "k8s", "javascript" not
          "js").
        - Only tag what the requirement actually names — never invent a tag it doesn't support.
        """;

    private const string CvTaggingSystemPrompt =
        """
        You are an expert technical recruiter tagging a candidate's CV with skill/technology/practice
        tags for matching purposes.

        You will be given a JSON object representing a candidate's parsed CV (roles, bullets, skills).

        Return a JSON object matching exactly this shape:
        {
          "Tags": [string, ...]
        }

        Rules:
        - At most 60 tags.
        - Each tag is a short (1 to 4 word) common name for a specific technology, practice, or
          skill (e.g. "kubernetes", "event-driven architecture", "mentoring") — use the most widely
          used full name rather than an abbreviation (e.g. "kubernetes" not "k8s", "javascript" not
          "js").
        - Only tag skills actually evidenced by the CV's roles/bullets/skills — never invent one.
        - Prefer specific tags (e.g. "react", "postgresql") over vague ones (e.g. "programming").
        """;

    private readonly FoundryOptions _foundryOptions = foundryOptions.Value;

    public async Task<List<List<string>>> TagRequirementsAsync(
        List<JdRequirement> requirements, CancellationToken cancellationToken = default)
    {
        if (requirements.Count == 0)
            return [];

        var userInput = JsonSerializer.Serialize(
            requirements.Select(r => new { r.Skill, r.Priority, r.YearsRequired, r.Notes }),
            InputJsonOptions);

        var completion = await foundryClient.GetStructuredCompletionAsync<RequirementTagsCompletion>(
            RequirementTaggingSystemPrompt, userInput, _foundryOptions.SkillTaggingDeploymentName, cancellationToken);

        var result = new List<List<string>>(requirements.Count);
        for (var i = 0; i < requirements.Count; i++)
        {
            var tags = i < completion.Tags.Count ? completion.Tags[i] : [];
            result.Add(TagCanonicalizer.CanonicalizeAll(tags));
        }

        return result;
    }

    public async Task<List<string>> TagCvAsync(CvDocument cv, CancellationToken cancellationToken = default)
    {
        var userInput = JsonSerializer.Serialize(cv, InputJsonOptions);

        var completion = await foundryClient.GetStructuredCompletionAsync<CvTagsCompletion>(
            CvTaggingSystemPrompt, userInput, _foundryOptions.SkillTaggingDeploymentName, cancellationToken);

        return TagCanonicalizer.CanonicalizeAll(completion.Tags).Take(MaxCvTags).ToList();
    }

    protected sealed class RequirementTagsCompletion
    {
        public List<List<string>> Tags { get; set; } = [];
    }

    protected sealed class CvTagsCompletion
    {
        public List<string> Tags { get; set; } = [];
    }
}
