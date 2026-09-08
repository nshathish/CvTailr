using CvTailr.Shared.Cv;
using CvTailr.Shared.Jd;
using CvTailr.Shared.Scoring;

namespace CvTailr.Web.Services;

// Scoped (per-circuit) state carried forward between workflow pages, since no
// component should hold parsed state only in local fields that vanish on
// navigation (see CvTailr.Web/CLAUDE.md). In-memory only — no persistence yet.
public class WorkflowStateService
{
    public JdRequirements? CurrentJd { get; set; }
    public CvDocument? CurrentCv { get; set; }
    public MatchScoreResult? CurrentScore { get; set; }
}
