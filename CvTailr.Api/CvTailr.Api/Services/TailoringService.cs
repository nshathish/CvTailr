using System.Text.Json;
using System.Text.Json.Serialization;
using CvTailr.Api.Clients;
using CvTailr.Api.Configuration;
using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Cv;
using CvTailr.Shared.Jd;
using CvTailr.Shared.Scoring;
using CvTailr.Shared.Tailoring;
using Microsoft.Extensions.Options;

namespace CvTailr.Api.Services;

public class TailoringService(
    IFoundryClient foundryClient,
    IOptions<FoundryOptions> foundryOptions,
    ILogger<TailoringService> logger) : ITailoringService
{
    // The only four languages the language-substitution rule may ever touch (root CLAUDE.md rule 2).
    private static readonly string[] AllowedLanguages = ["Python", "C#", "TypeScript", "Java"];

    private static readonly JsonSerializerOptions InputJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private const string SystemPrompt = """
                                        You are a CV tailoring assistant. You will be given a JSON object describing a
                                        candidate's CV and a job description, with these properties:
                                        - "Bullets": a flat list of the candidate's existing CV bullets, each
                                          { RoleId, BulletId, OriginalText, Language }. Company names and dates are
                                          deliberately NOT included in this data — you have no access to them and must
                                          never invent, infer, or reference one in any field you return.
                                        - "ExistingSkills": skill names already listed on the CV.
                                        - "JdRequirements": the job description's requirements (Skill, Priority,
                                          YearsRequired, Notes).
                                        - "EmphasizedLanguages": languages (drawn only from Python, C#, TypeScript, Java)
                                          that the job description emphasizes.
                                        - "GapsToAddress": skill names identified as gaps between the CV and the job
                                          description.

                                        Propose CV tailoring changes and return a JSON object matching exactly this shape:
                                        {
                                          "BulletRewrites": [
                                            { "RoleId": string, "BulletId": string, "ProposedText": string, "ProposedLanguage": string | null, "Reason": string }
                                          ],
                                          "NewBullets": [
                                            { "RoleId": string, "ProposedText": string, "ProposedLanguage": string | null, "Reason": string }
                                          ],
                                          "NewSkills": [
                                            { "SkillName": string, "Reason": string }
                                          ]
                                        }

                                        Rules:
                                        - Every RoleId/BulletId you return MUST be copied exactly from the input
                                          Bullets list — never invent one.
                                        - Only set "ProposedLanguage" on a bullet rewrite when proposing a genuine
                                          language substitution: the bullet's current Language must be exactly one of
                                          Python, C#, TypeScript, or Java, AND EmphasizedLanguages must contain a
                                          DIFFERENT one of those same four languages, AND ProposedText must be reworded
                                          to reflect that new language. Leave ProposedLanguage null for ordinary wording
                                          improvements that don't change the bullet's language.
                                        - Never propose a language substitution to or from any language outside
                                          Python/C#/TypeScript/Java, and never propose one unless EmphasizedLanguages
                                          justifies it.
                                        - Only propose NewBullets/NewSkills that plausibly address an item in
                                          GapsToAddress and are a believable extension of the candidate's actual
                                          experience — never fabricate unrelated experience.
                                        - Do not propose a new skill whose name already appears in ExistingSkills.
                                        - Never mention, infer, or reference a company name or a date anywhere in your
                                          response.
                                        """;

    private readonly FoundryOptions _foundryOptions = foundryOptions.Value;

    public async Task<TailoringProposal> ProposeAsync(
        CvDocument cvDocument,
        JdRequirements jdRequirements,
        MatchScoreResult? scoreResult,
        CancellationToken cancellationToken = default)
    {
        var bulletsById = cvDocument.Roles
            .SelectMany(role => role.Bullets.Select(bullet => (role, bullet)))
            .ToDictionary(x => x.bullet.Id, x => x);

        var roleIds = cvDocument.Roles.Select(r => r.Id).ToHashSet();

        var promptInput = new
        {
            Bullets = bulletsById.Values.Select(x => new
            {
                RoleId = x.role.Id,
                BulletId = x.bullet.Id,
                x.bullet.OriginalText,
                x.bullet.Language
            }),
            ExistingSkills = cvDocument.Skills.Select(s => s.Name),
            JdRequirements = jdRequirements.Requirements,
            jdRequirements.EmphasizedLanguages,
            GapsToAddress = scoreResult is not null
                ? scoreResult.MissingMustHaves.ToList()
                : jdRequirements.Requirements.Select(r => r.Skill).ToList()
        };

        var userInput = JsonSerializer.Serialize(promptInput, InputJsonOptions);

        var completion = await foundryClient.GetStructuredCompletionAsync<TailoringCompletion>(
            SystemPrompt,
            userInput,
            _foundryOptions.ScoringDeploymentName,
            cancellationToken);

        var bulletRewrites = new List<BulletRewriteProposal>();
        foreach (var candidate in completion.BulletRewrites)
        {
            if (!bulletsById.TryGetValue(candidate.BulletId, out var found) || found.role.Id != candidate.RoleId)
            {
                logger.LogWarning(
                    "Foundry proposed a rewrite for unknown RoleId/BulletId {RoleId}/{BulletId}; discarding.",
                    candidate.RoleId, candidate.BulletId);
                continue;
            }

            if (candidate.ProposedLanguage is not null &&
                !IsValidLanguageSubstitution(found.bullet.Language, candidate.ProposedLanguage, jdRequirements))
            {
                // The rewritten text is presumed to embody the (invalid) language substitution itself,
                // not just the ProposedLanguage tag — so discard the whole rewrite rather than only the
                // language field, per the hard rule that scopes substitutions to these four languages.
                logger.LogWarning(
                    "Discarding proposed language substitution {From} -> {To} for bullet {BulletId}: outside the allowed language set or not justified by EmphasizedLanguages.",
                    found.bullet.Language, candidate.ProposedLanguage, candidate.BulletId);
                continue;
            }

            bulletRewrites.Add(new BulletRewriteProposal
            {
                RoleId = candidate.RoleId,
                BulletId = candidate.BulletId,
                CurrentText = found.bullet.OriginalText,
                ProposedText = candidate.ProposedText,
                ProposedLanguage = candidate.ProposedLanguage,
                Reason = candidate.Reason
            });
        }

        var newBullets = new List<NewBulletProposal>();
        foreach (var candidate in completion.NewBullets)
        {
            if (!roleIds.Contains(candidate.RoleId))
            {
                logger.LogWarning(
                    "Foundry proposed a new bullet for unknown RoleId {RoleId}; discarding.", candidate.RoleId);
                continue;
            }

            newBullets.Add(new NewBulletProposal
            {
                RoleId = candidate.RoleId,
                ProposedText = candidate.ProposedText,
                ProposedLanguage = candidate.ProposedLanguage,
                Reason = candidate.Reason
            });
        }

        var existingSkillNames = cvDocument.Skills
            .Select(s => s.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newSkills = completion.NewSkills
            .Where(candidate => !existingSkillNames.Contains(candidate.SkillName))
            .Select(candidate => new NewSkillProposal
            {
                SkillName = candidate.SkillName,
                Reason = candidate.Reason
            })
            .ToList();

        return new TailoringProposal
        {
            CvId = cvDocument.Id,
            JdId = jdRequirements.Id,
            BulletRewrites = bulletRewrites,
            NewBullets = newBullets,
            NewSkills = newSkills
        };
    }

    public TailorApplyResult ApplyAsync(
        CvDocument cvDocument,
        List<string> approvedBulletRewriteIds,
        List<BulletRewriteProposal> approvedRewrites,
        List<NewBulletProposal> approvedNewBullets,
        List<NewSkillProposal> approvedNewSkills)
    {
        var warnings = new List<string>();
        var bulletsById = cvDocument.Roles
            .SelectMany(role => role.Bullets)
            .ToDictionary(b => b.Id);
        var rolesById = cvDocument.Roles.ToDictionary(r => r.Id);

        var approvedRewriteIds = approvedBulletRewriteIds.ToHashSet();
        foreach (var rewrite in approvedRewrites.Where(r => approvedRewriteIds.Contains(r.Id)))
        {
            if (!bulletsById.TryGetValue(rewrite.BulletId, out var bullet) || bullet is null ||
                !rolesById.ContainsKey(rewrite.RoleId))
            {
                warnings.Add($"Skipped bullet rewrite {rewrite.Id}: RoleId/BulletId not found in the given CvDocument.");
                continue;
            }

            bullet.TailoredText = rewrite.ProposedText;
            if (rewrite.ProposedLanguage is not null)
            {
                bullet.Language = rewrite.ProposedLanguage;
            }
        }

        foreach (var newBullet in approvedNewBullets)
        {
            if (!rolesById.TryGetValue(newBullet.RoleId, out var role))
            {
                warnings.Add($"Skipped new bullet {newBullet.Id}: RoleId {newBullet.RoleId} not found in the given CvDocument.");
                continue;
            }

            role.Bullets.Add(new CvBullet
            {
                OriginalText = newBullet.ProposedText,
                Language = newBullet.ProposedLanguage,
                IsProvisional = true
            });
        }

        foreach (var newSkill in approvedNewSkills)
        {
            cvDocument.Skills.Add(new CvSkill
            {
                Name = newSkill.SkillName,
                IsProvisional = true
            });
        }

        return new TailorApplyResult(cvDocument, warnings);
    }

    private static bool IsValidLanguageSubstitution(string? currentLanguage, string proposedLanguage, JdRequirements jdRequirements) =>
        currentLanguage is not null &&
        AllowedLanguages.Contains(currentLanguage) &&
        AllowedLanguages.Contains(proposedLanguage) &&
        proposedLanguage != currentLanguage &&
        jdRequirements.EmphasizedLanguages.Contains(proposedLanguage);

    private class TailoringCompletion
    {
        public List<BulletRewriteCandidate> BulletRewrites { get; set; } = new();
        public List<NewBulletCandidate> NewBullets { get; set; } = new();
        public List<NewSkillCandidate> NewSkills { get; set; } = new();
    }

    private class BulletRewriteCandidate
    {
        public string RoleId { get; set; } = string.Empty;
        public string BulletId { get; set; } = string.Empty;
        public string ProposedText { get; set; } = string.Empty;
        public string? ProposedLanguage { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    private class NewBulletCandidate
    {
        public string RoleId { get; set; } = string.Empty;
        public string ProposedText { get; set; } = string.Empty;
        public string? ProposedLanguage { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    private class NewSkillCandidate
    {
        public string SkillName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
