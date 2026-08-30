using System.Text.Json;
using System.Text.Json.Serialization;
using CvTailr.Api.Clients;
using CvTailr.Api.Configuration;
using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Cv;
using CvTailr.Shared.Enums;
using CvTailr.Shared.Jd;
using CvTailr.Shared.Scoring;
using Microsoft.Extensions.Options;

namespace CvTailr.Api.Services;

public class ScoringService(
    IFoundryClient foundryClient,
    IOptions<FoundryOptions> foundryOptions,
    ILogger<ScoringService> logger) : IScoringService
{
    private const int MustHavePoints = 3;
    private const int NiceToHavePoints = 1;

    private static readonly JsonSerializerOptions InputJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private const string SystemPrompt = """
                                        You are an expert technical recruiter assessing how well a candidate's CV matches a
                                        job description's requirements.

                                        You will be given a JSON object with two properties: "JdRequirements" (the parsed
                                        job description requirements) and "CvDocument" (the candidate's parsed CV).

                                        For EVERY item in JdRequirements.Requirements, assess whether the CV evidences that
                                        requirement, and return a JSON object matching exactly this shape:
                                        {
                                          "RequirementMatches": [
                                            {
                                              "RequirementId": string,  // must exactly match the input requirement's Id
                                              "Skill": string,          // copy the input requirement's Skill
                                              "IsMet": bool,
                                              "Confidence": number,     // 0-100: how strongly the CV evidences this
                                              "SupportingEvidence": string | null,  // the CV bullet or skill that supports this, if met
                                              "GapNote": string | null  // short note on what's missing, if not met
                                            }
                                          ],
                                          "Rationale": string  // 2-4 sentences summarizing overall fit: the strongest
                                                                // matches and the most significant gaps. Do not mention
                                                                // or restate a percentage score.
                                        }

                                        Rules:
                                        - Return exactly one RequirementMatch per input requirement, in any order, but
                                          every RequirementId from the input must appear exactly once in the output.
                                        - SupportingEvidence must be null when IsMet is false. GapNote must be null when
                                          IsMet is true.
                                        - Base your assessment only on the CV content actually provided — never assume a
                                          skill is present unless it's listed or evidenced by a bullet.
                                        """;

    private readonly FoundryOptions _foundryOptions = foundryOptions.Value;

    public async Task<MatchScoreResult> ScoreAsync(
        JdRequirements jdRequirements,
        CvDocument cvDocument,
        CancellationToken cancellationToken = default)
    {
        var userInput = JsonSerializer.Serialize(
            new { JdRequirements = jdRequirements, CvDocument = cvDocument },
            InputJsonOptions);

        var completion = await foundryClient.GetStructuredCompletionAsync<ScoringCompletion>(
            SystemPrompt,
            userInput,
            _foundryOptions.ScoringDeploymentName,
            cancellationToken);

        var matches = ReconcileMatches(jdRequirements.Requirements, completion.RequirementMatches);

        return new MatchScoreResult
        {
            CvId = cvDocument.Id,
            JdId = jdRequirements.Id,
            OverallScorePercent = CalculateOverallScore(matches, jdRequirements.Requirements),
            Rationale = completion.Rationale,
            RequirementMatches = matches
        };
    }

    private List<RequirementMatch> ReconcileMatches(
        List<JdRequirement> requirements,
        List<RequirementMatch> matches)
    {
        var matchesByRequirementId = matches
            .GroupBy(m => m.RequirementId)
            .ToDictionary(g => g.Key, g => g.First());

        var reconciled = new List<RequirementMatch>(requirements.Count);

        foreach (var requirement in requirements)
        {
            if (matchesByRequirementId.TryGetValue(requirement.Id, out var match))
            {
                reconciled.Add(match);
                continue;
            }

            logger.LogWarning(
                "Foundry scoring response did not include a match for requirement {RequirementId} ({Skill}); synthesizing a default unmet match.",
                requirement.Id, requirement.Skill);

            reconciled.Add(new RequirementMatch
            {
                RequirementId = requirement.Id,
                Skill = requirement.Skill,
                IsMet = false,
                Confidence = 0,
                GapNote = "Not assessed"
            });
        }

        return reconciled;
    }

    private static int CalculateOverallScore(List<RequirementMatch> matches, List<JdRequirement> requirements)
    {
        var matchesByRequirementId = matches.ToDictionary(m => m.RequirementId);

        var pointsEarned = 0;
        var maxPossiblePoints = 0;

        foreach (var requirement in requirements)
        {
            var points = requirement.Priority == RequirementPriority.MustHave ? MustHavePoints : NiceToHavePoints;
            maxPossiblePoints += points;

            if (matchesByRequirementId.TryGetValue(requirement.Id, out var match) && match.IsMet)
            {
                pointsEarned += points;
            }
        }

        return maxPossiblePoints == 0 ? 0 : (int)Math.Round(100.0 * pointsEarned / maxPossiblePoints);
    }

    private class ScoringCompletion
    {
        public List<RequirementMatch> RequirementMatches { get; set; } = new();
        public string Rationale { get; set; } = string.Empty;
    }
}
