using CvTailr.Api.Data.Interfaces;
using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Cv;
using CvTailr.Shared.Ledger;

namespace CvTailr.Api.Services;

public class LedgerService(
    ILedgerRepository ledgerRepository,
    ITailoredCvRepository tailoredCvRepository,
    ILogger<LedgerService> logger) : ILedgerService
{
    // Mirrors LedgerEntry.HasRecentWeakPerformance's own defaults, made explicit here so the
    // threshold is visible and tunable at the call site rather than hidden in a default parameter.
    private const int WeakPerformanceLookback = 3;
    private const int WeakPerformanceFailThreshold = 2;

    // Promotion requires this many of the most recent attempts to all be Pass.
    private const int PromotionLookback = 3;

    public async Task<List<LedgerEntry>> RegisterProvisionalItemsAsync(
        string cvId, string jobId, TailoredCvDocument tailoredDocument, CancellationToken cancellationToken = default)
    {
        var existingEntries = await ledgerRepository.GetByCvAndJobIdAsync(cvId, jobId, cancellationToken);
        var existingKeys = existingEntries
            .Select(e => (e.SubjectName, e.RelatedBulletId))
            .ToHashSet();

        var newEntries = new List<LedgerEntry>();

        foreach (var skill in tailoredDocument.Skills.Where(s => s.IsProvisional))
        {
            var key = (skill.Name, (string?)null);
            if (!existingKeys.Add(key))
            {
                continue;
            }

            newEntries.Add(new LedgerEntry
            {
                CvId = cvId,
                JobId = jobId,
                SubjectName = skill.Name,
                RelatedBulletId = null,
                Status = LedgerStatus.Provisional
            });
        }

        foreach (var bullet in tailoredDocument.Roles.SelectMany(role => role.Bullets).Where(b => b.IsProvisional))
        {
            var key = (bullet.OriginalText, (string?)bullet.Id);
            if (!existingKeys.Add(key))
            {
                continue;
            }

            newEntries.Add(new LedgerEntry
            {
                CvId = cvId,
                JobId = jobId,
                SubjectName = bullet.OriginalText,
                RelatedBulletId = bullet.Id,
                Status = LedgerStatus.Provisional
            });
        }

        foreach (var entry in newEntries)
        {
            await ledgerRepository.AddAsync(entry, cancellationToken);
        }

        return existingEntries.Concat(newEntries).ToList();
    }

    public Task<List<LedgerEntry>> GetByCvIdAsync(string cvId, CancellationToken cancellationToken = default) =>
        ledgerRepository.GetByCvIdAsync(cvId, cancellationToken);

    public Task<List<LedgerEntry>> GetByCvAndJobIdAsync(string cvId, string jobId, CancellationToken cancellationToken = default) =>
        ledgerRepository.GetByCvAndJobIdAsync(cvId, jobId, cancellationToken);

    public async Task<LedgerEntry> RecordDrillAttemptAsync(string ledgerEntryId, DrillAttempt attempt, CancellationToken cancellationToken = default)
    {
        var entry = await GetRequiredEntryAsync(ledgerEntryId, cancellationToken);

        entry.DrillHistory.Add(attempt);
        entry.LastReviewed = DateTimeOffset.UtcNow;

        await ledgerRepository.UpdateAsync(entry, cancellationToken);
        return entry;
    }

    public async Task<LedgerReviewRecommendation> ReviewAsync(string ledgerEntryId, CancellationToken cancellationToken = default)
    {
        var entry = await GetRequiredEntryAsync(ledgerEntryId, cancellationToken);

        if (entry.Status != LedgerStatus.Provisional)
        {
            return new LedgerReviewRecommendation
            {
                LedgerEntryId = entry.Id,
                RecommendedAction = RecommendedAction.NoChange,
                Reason = $"Entry status is {entry.Status}, not Provisional; not subject to downgrade/promotion review."
            };
        }

        if (entry.HasRecentWeakPerformance(WeakPerformanceLookback, WeakPerformanceFailThreshold))
        {
            var recent = entry.DrillHistory
                .OrderByDescending(a => a.Timestamp)
                .Take(WeakPerformanceLookback)
                .ToList();
            var weakOrFailCount = recent.Count(a => a.Outcome != DrillOutcome.Pass);

            return new LedgerReviewRecommendation
            {
                LedgerEntryId = entry.Id,
                RecommendedAction = RecommendedAction.RecommendDowngrade,
                Reason = $"{weakOrFailCount} of last {recent.Count} drills were Weak or Fail."
            };
        }

        var mostRecent = entry.DrillHistory
            .OrderByDescending(a => a.Timestamp)
            .Take(PromotionLookback)
            .ToList();

        if (entry.DrillHistory.Count >= PromotionLookback && mostRecent.All(a => a.Outcome == DrillOutcome.Pass))
        {
            return new LedgerReviewRecommendation
            {
                LedgerEntryId = entry.Id,
                RecommendedAction = RecommendedAction.RecommendPromote,
                Reason = $"Last {PromotionLookback} drills were all Pass."
            };
        }

        return new LedgerReviewRecommendation
        {
            LedgerEntryId = entry.Id,
            RecommendedAction = RecommendedAction.NoChange,
            Reason = "No consistent pattern of weak performance or sustained passes yet."
        };
    }

    public async Task<LedgerEntry> ConfirmStatusChangeAsync(string ledgerEntryId, LedgerStatus newStatus, CancellationToken cancellationToken = default)
    {
        var entry = await GetRequiredEntryAsync(ledgerEntryId, cancellationToken);

        logger.LogInformation(
            "Confirming ledger status change for {LedgerEntryId}: {OldStatus} -> {NewStatus}.",
            entry.Id, entry.Status, newStatus);

        entry.Status = newStatus;
        entry.LastReviewed = DateTimeOffset.UtcNow;

        await ledgerRepository.UpdateAsync(entry, cancellationToken);

        if (newStatus is LedgerStatus.Confirmed or LedgerStatus.Removed)
        {
            await ApplyStatusChangeToTailoredDocumentAsync(entry, newStatus, cancellationToken);
        }

        return entry;
    }

    // Per root CLAUDE.md's deliberate design decision: Confirmed/Removed only ever affect the
    // job's own TailoredCvDocument, never the master CvDocument, and never merge across jobs.
    private async Task ApplyStatusChangeToTailoredDocumentAsync(LedgerEntry entry, LedgerStatus newStatus, CancellationToken cancellationToken)
    {
        var tailoredDocument = await tailoredCvRepository.GetByJobIdAsync(entry.JobId, cancellationToken);
        if (tailoredDocument is null)
        {
            logger.LogInformation(
                "No TailoredCvDocument exists yet for job {JobId} — nothing to update for ledger entry {LedgerEntryId}.",
                entry.JobId, entry.Id);
            return;
        }

        var changed = false;

        if (entry.RelatedBulletId is not null)
        {
            foreach (var role in tailoredDocument.Roles)
            {
                var bullet = role.Bullets.FirstOrDefault(b => b.Id == entry.RelatedBulletId);
                if (bullet is null)
                {
                    continue;
                }

                if (newStatus == LedgerStatus.Removed)
                {
                    role.Bullets.Remove(bullet);
                }
                else
                {
                    bullet.IsProvisional = false;
                }

                changed = true;
                break;
            }
        }
        else
        {
            var skill = tailoredDocument.Skills.FirstOrDefault(s => s.Name == entry.SubjectName);
            if (skill is not null)
            {
                if (newStatus == LedgerStatus.Removed)
                {
                    tailoredDocument.Skills.Remove(skill);
                }
                else
                {
                    skill.IsProvisional = false;
                }

                changed = true;
            }
        }

        if (changed)
        {
            tailoredDocument.UpdatedAt = DateTimeOffset.UtcNow;
            await tailoredCvRepository.UpsertAsync(tailoredDocument, cancellationToken);
        }
    }

    private async Task<LedgerEntry> GetRequiredEntryAsync(string ledgerEntryId, CancellationToken cancellationToken)
    {
        return await ledgerRepository.GetByIdAsync(ledgerEntryId, cancellationToken)
            ?? throw new KeyNotFoundException($"LedgerEntry '{ledgerEntryId}' was not found.");
    }
}
