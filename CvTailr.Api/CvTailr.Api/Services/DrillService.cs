using System.Text.Json;
using System.Text.Json.Serialization;
using CvTailr.Api.Clients;
using CvTailr.Api.Configuration;
using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Cv;
using CvTailr.Shared.Drill;
using CvTailr.Shared.Enums;
using CvTailr.Shared.Jd;
using CvTailr.Shared.Ledger;
using Microsoft.Extensions.Options;

namespace CvTailr.Api.Services;

public class DrillService(
    IFoundryClient foundryClient,
    IOptions<FoundryOptions> foundryOptions,
    ILedgerService ledgerService) : IDrillService
{
    // Weighted candidate pool tuning (root CLAUDE.md's continuous-feedback design): provisional
    // items are the least-proven claims and should come up most; confirmed content "shouldn't rot"
    // but shouldn't dominate either.
    private const int ProvisionalEntryWeight = 3;
    private const int JdGapWeight = 2;
    private const int ConfirmedBulletWeight = 1;

    private static readonly JsonSerializerOptions InputJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private const string EvaluationSystemPrompt = """
                                                  You are an expert technical interviewer evaluating a candidate's answer to an
                                                  interview question. The answer may have been typed directly or transcribed from
                                                  speech — evaluate the content, not the modality.

                                                  Judge the answer on BOTH:
                                                  - Technical/factual correctness and depth.
                                                  - Communication quality: structure, conciseness, and whether it actually
                                                    answers what was asked. A technically correct but rambling, unstructured, or
                                                    off-target answer should NOT be scored as a clean Pass.

                                                  Return a JSON object matching exactly this shape:
                                                  {
                                                    "Outcome": "Pass" | "Weak" | "Fail",
                                                    "Feedback": string,  // 1-3 sentences: what was good, what was missing
                                                    "SuggestedFollowUp": string | null  // a clarifying follow-up question if
                                                                                          // Outcome is "Weak"; null otherwise
                                                  }
                                                  """;

    private readonly FoundryOptions _foundryOptions = foundryOptions.Value;

    public async Task<DrillQuestion> GenerateQuestionAsync(
        CvDocument cvDocument,
        JdRequirements jdRequirements,
        List<LedgerEntry> ledgerEntries,
        CancellationToken cancellationToken = default)
    {
        var pool = BuildCandidatePool(cvDocument, jdRequirements, ledgerEntries);
        var selected = pool.Count > 0
            ? PickWeighted(pool)
            : new Candidate(CandidateKind.Fallback, string.Empty, null, null);

        var (systemPrompt, targetType) = BuildQuestionPrompt(selected);
        var userInput = string.IsNullOrEmpty(selected.ContextText) ? "N/A" : selected.ContextText;

        var completion = await foundryClient.GetStructuredCompletionAsync<QuestionCompletion>(
            systemPrompt,
            userInput,
            _foundryOptions.ScoringDeploymentName,
            cancellationToken);

        return new DrillQuestion
        {
            Prompt = completion.Prompt,
            TargetType = targetType,
            RelatedLedgerEntryId = selected.RelatedLedgerEntryId,
            RelatedRequirementId = selected.RelatedRequirementId
        };
    }

    public async Task<DrillAnswerEvaluation> EvaluateAnswerAsync(
        DrillQuestion question,
        string answerText,
        CancellationToken cancellationToken = default)
    {
        var userInput = JsonSerializer.Serialize(
            new { question.Prompt, question.TargetType, AnswerText = answerText },
            InputJsonOptions);

        var completion = await foundryClient.GetStructuredCompletionAsync<AnswerEvaluationCompletion>(
            EvaluationSystemPrompt,
            userInput,
            _foundryOptions.ScoringDeploymentName,
            cancellationToken);

        var evaluation = new DrillAnswerEvaluation
        {
            DrillQuestionId = question.Id,
            Outcome = completion.Outcome,
            Feedback = completion.Feedback,
            // Defensive backstop, same reasoning as elsewhere: don't trust the model to have
            // respected the "null unless Weak" rule on its own.
            SuggestedFollowUp = completion.Outcome == DrillOutcome.Weak ? completion.SuggestedFollowUp : null
        };

        if (question.RelatedLedgerEntryId is not null)
        {
            await ledgerService.RecordDrillAttemptAsync(
                question.RelatedLedgerEntryId,
                new DrillAttempt
                {
                    Outcome = evaluation.Outcome,
                    Notes = evaluation.Feedback,
                    Timestamp = DateTimeOffset.UtcNow
                },
                cancellationToken);
        }

        return evaluation;
    }

    private static List<(Candidate Candidate, int Weight)> BuildCandidatePool(
        CvDocument cvDocument,
        JdRequirements jdRequirements,
        List<LedgerEntry> ledgerEntries)
    {
        var pool = new List<(Candidate, int)>();

        foreach (var entry in ledgerEntries.Where(e => e.Status == LedgerStatus.Provisional))
        {
            var kind = entry.RelatedBulletId is not null ? CandidateKind.ProvisionalBullet : CandidateKind.ProvisionalSkill;
            pool.Add((new Candidate(kind, entry.SubjectName, entry.Id, null), ProvisionalEntryWeight));
        }

        foreach (var requirement in jdRequirements.Requirements.Where(r => r.Priority == RequirementPriority.MustHave))
        {
            pool.Add((new Candidate(CandidateKind.JdGap, requirement.Skill, null, requirement.Id), JdGapWeight));
        }

        foreach (var bullet in cvDocument.Roles.SelectMany(role => role.Bullets).Where(b => !b.IsProvisional))
        {
            var contextText = bullet.TailoredText ?? bullet.OriginalText;
            pool.Add((new Candidate(CandidateKind.ConfirmedBullet, contextText, null, null), ConfirmedBulletWeight));
        }

        return pool;
    }

    private static Candidate PickWeighted(List<(Candidate Candidate, int Weight)> pool)
    {
        var totalWeight = pool.Sum(p => p.Weight);
        var roll = Random.Shared.Next(totalWeight);

        var cumulative = 0;
        foreach (var (candidate, weight) in pool)
        {
            cumulative += weight;
            if (roll < cumulative)
            {
                return candidate;
            }
        }

        return pool[^1].Candidate;
    }

    private static (string SystemPrompt, DrillTargetType TargetType) BuildQuestionPrompt(Candidate candidate) =>
        candidate.Kind switch
        {
            CandidateKind.ProvisionalBullet => ("""
                You are an interview coach preparing a defend-the-claim behavioral question. The
                candidate's CV includes a claimed accomplishment that hasn't yet been proven out in
                practice interviews. Ask ONE behavioral question that asks the candidate to walk
                through this specific experience in real detail — as a real interviewer verifying a
                claim would, not a generic prompt.

                Return a JSON object matching exactly this shape: { "Prompt": string }
                """, DrillTargetType.Behavioral),

            CandidateKind.ProvisionalSkill => ("""
                You are an interview coach preparing a technical depth-check question. The candidate's
                CV claims proficiency in a skill that hasn't yet been proven out in practice
                interviews. Ask ONE technical question that tests real depth in this skill, not just
                surface familiarity.

                Return a JSON object matching exactly this shape: { "Prompt": string }
                """, DrillTargetType.Technical),

            CandidateKind.JdGap => ("""
                You are an interview coach preparing a gap-probe question. The job description
                requires a skill the candidate's CV doesn't clearly evidence. Ask ONE question that
                directly tests the candidate's ability in this skill, framed the way a real
                interviewer would probe a suspected weak area — not accusatory, just direct.

                Return a JSON object matching exactly this shape: { "Prompt": string }
                """, DrillTargetType.GapProbe),

            CandidateKind.ConfirmedBullet => ("""
                You are an interview coach preparing a standard behavioral question. Ask ONE
                "tell me about a time" style behavioral question based on the following CV bullet, so
                the candidate's confirmed experience stays sharp for real interviews.

                Return a JSON object matching exactly this shape: { "Prompt": string }
                """, DrillTargetType.Behavioral),

            _ => ("""
                You are an interview coach. Ask ONE general behavioral interview question suitable for
                a software engineering candidate.

                Return a JSON object matching exactly this shape: { "Prompt": string }
                """, DrillTargetType.Behavioral)
        };

    private enum CandidateKind
    {
        ProvisionalBullet,
        ProvisionalSkill,
        JdGap,
        ConfirmedBullet,
        Fallback
    }

    private record Candidate(CandidateKind Kind, string ContextText, string? RelatedLedgerEntryId, string? RelatedRequirementId);

    private class QuestionCompletion
    {
        public string Prompt { get; set; } = string.Empty;
    }

    private class AnswerEvaluationCompletion
    {
        public DrillOutcome Outcome { get; set; }
        public string Feedback { get; set; } = string.Empty;
        public string? SuggestedFollowUp { get; set; }
    }
}
