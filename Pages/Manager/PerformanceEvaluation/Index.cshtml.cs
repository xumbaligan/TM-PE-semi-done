using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.Manager.PerformanceEvaluation
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        public IndexModel(AppDbContext context) => _context = context;

        public List<Model.PerformanceEvaluation> Evaluations { get; set; } = new();

        // Performance snapshot (Completed/On-time/Rescheduled-or-Rejected/
        // Cancelled-or-Overdue) for the shared "view full evaluation" modal -
        // scoped to each evaluation's own period, not an all-time total, since
        // the modal is about what happened during that specific period. Keyed
        // by EvaluationID since a period's stats can differ per evaluation.
        public Dictionary<int, EmployeePerformanceStats> EvaluationStats { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        public async Task OnGetAsync()
        {
            var query = _context.PerformanceEvaluations
                .Include(e => e.Employee)
                .Include(e => e.Results).ThenInclude(r => r.Criteria)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(StatusFilter) &&
                Enum.TryParse<EvaluationStatus>(StatusFilter, true, out var status))
            {
                query = query.Where(e => e.EvaluationStatus == status);
            }

            if (!string.IsNullOrWhiteSpace(Search))
            {
                var term = Search.Trim();
                query = query.Where(e =>
                    (e.Employee != null && e.Employee.FullName.Contains(term)) ||
                    e.EvaluatorName.Contains(term));
            }

            Evaluations = await query
                .OrderByDescending(e => e.EvaluationDate)
                .ThenByDescending(e => e.EvaluationID)
                .ToListAsync();

            // Batched per distinct period rather than per evaluation - many
            // evaluations across different employees commonly share the same
            // period (e.g. everyone evaluated for "August 2026"), even though
            // each employee only ever has one evaluation per period.
            foreach (var periodGroup in Evaluations.GroupBy(e => (e.EvaluationPeriodMonth, e.EvaluationPeriodYear)))
            {
                var sample = periodGroup.First();
                var periodStats = await EmployeePerformanceStatsBuilder.BuildAsync(
                    _context, periodGroup.Select(e => e.EmployeeID),
                    sample.EvaluationPeriodStart, sample.EvaluationPeriodEnd);

                foreach (var e in periodGroup)
                {
                    EvaluationStats[e.EvaluationID] = periodStats.GetValueOrDefault(e.EmployeeID) ?? new EmployeePerformanceStats();
                }
            }
        }

        // Small DTOs for the View modal - only what the modal needs, serialized
        // straight into the button's data-* attributes.
        public class ResultView
        {
            public string CriteriaName { get; set; } = string.Empty;
            public decimal Weight { get; set; }
            public decimal Stars { get; set; }
            public decimal Score { get; set; }
            public string? Feedback { get; set; }
        }

        // Shared by the Index modal and the Appraisal Records details modal so
        // both render an evaluation exactly the same way.
        public static List<ResultView> BuildResultViews(Model.PerformanceEvaluation e) =>
            e.Results
                .OrderByDescending(r => r.Criteria?.Weight ?? 0)
                .Select(r => new ResultView
                {
                    CriteriaName = r.Criteria?.CriteriaName ?? "-",
                    Weight = r.Criteria?.Weight ?? 0,
                    Stars = r.StarRating,
                    Score = r.Score,
                    Feedback = r.Feedback
                })
                .ToList();
    }
}
