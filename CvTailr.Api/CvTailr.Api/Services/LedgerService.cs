using CvTailr.Api.Data;
using CvTailr.Api.Services.Interfaces;
using CvTailr.Shared.Cv;
using CvTailr.Shared.Ledger;

namespace CvTailr.Api.Services;

public class LedgerService(ILedgerRepository ledgerRepository, ILogger<LedgerService> logger) : ILedgerService
{
    // Mirrors LedgerEntry.HasRecentWeakPerformance's own defaults, made explicit here so the
    // threshold is visible and tunable at the call site rather than hidden in a default parameter.
    private const int WeakPerformanceLookback = 3;
    private const int WeakPerformanceFailThreshold = 2;

    // Promotion requires this many of the most recent attempts to all be Pass.
    private const int PromotionLookback = 3;

    public async Task<List<LedgerEntry>> RegisterProvisionalItemsAsync(CvDocument cvDocument, CancellationToken cancellationToken = default)
    {
        var existingEntries = await ledgerRepository.GetByCvIdAsync(cvDocument.Id, cancellationToken);
        var existingKeys = existingEntries
            .Select(e => (e.SubjectName, e.RelatedBulletId))
            .ToHashSet();

        var newEntries = new List<LedgerEntry>();

        foreach (var skill in cvDocument.Skills.Where(s => s.IsProvisional))
        {
            var key = (skill.Name, (string?)null);
            if (!existingKeys.Add(key))
            {
                continue;
            }

            newEntries.Add(new LedgerEntry
            {
                CvId = cvDocument.Id,
                SubjectName = skill.Name,
                RelatedBulletId = null,
                Status = LedgerStatus.Provisional
            });
        }

        foreach (var bullet in cvDocument.Roles.SelectMany(role => role.Bullets).Where(b => b.IsProvisional))
        {
            var key = (bullet.OriginalText, (string?)bullet.Id);
            if (!existingKeys.Add(key))
            {
                continue;
            }

            newEntries.Add(new LedgerEntry
            {
                CvId = cvDocument.Id,
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
        return entry;
    }

    private async Task<LedgerEntry> GetRequiredEntryAsync(string ledgerEntryId, CancellationToken cancellationToken)
    {
        return await ledgerRepository.GetByIdAsync(ledgerEntryId, cancellationToken)
            ?? throw new KeyNotFoundException($"LedgerEntry '{ledgerEntryId}' was not found.");
    }
}
