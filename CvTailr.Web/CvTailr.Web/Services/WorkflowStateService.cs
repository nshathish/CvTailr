using CvTailr.Shared.Cv;
using CvTailr.Shared.Jobs;

namespace CvTailr.Web.Services;

// Scoped (per-circuit) state carried forward between workflow pages, since no
// component should hold parsed state only in local fields that vanish on
// navigation (see CvTailr.Web/CLAUDE.md). In-memory only — no persistence yet.
public class WorkflowStateService
{
    public Job? CurrentJob { get; set; }
    public CvDocument? CurrentCv { get; set; }
}
