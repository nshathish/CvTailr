using CvTailr.Shared.Scoring;

namespace CvTailr.Web.Services;

// Web-only presentation state for a RequirementMatch — the Api result only carries IsMet/
// Confidence/SupportingEvidence, so this is derived client-side rather than a stored field
// (see task 015: a Partial/MatchLevel field on RequirementMatch is explicitly out of scope).
public enum RequirementState
{
    Met,
    MetLowConfidence,
    Partial,
    Gap
}

public static class RequirementClassifier
{
    public static RequirementState Classify(RequirementMatch match) => match switch
    {
        { IsMet: true, Confidence: >= 70 } => RequirementState.Met,
        { IsMet: true } => RequirementState.MetLowConfidence,
        { IsMet: false } when !string.IsNullOrWhiteSpace(match.SupportingEvidence) => RequirementState.Partial,
        _ => RequirementState.Gap
    };

    public static bool IsToWorkOn(RequirementState state) =>
        state is RequirementState.Gap or RequirementState.Partial or RequirementState.MetLowConfidence;
}
