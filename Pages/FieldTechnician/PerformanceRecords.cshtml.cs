using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.FieldTechnician
{
    // Read-only "My Performance Records" for the logged-in Field Technician:
    // their own evaluation history and appraisal feedback trail. Mirrors
    // Manager/AppraisalRecords/Details, scoped to the current session's
    // employee instead of an {id} route param, and with nothing to edit -
    // a technician can only ever view their own record here.
    public class PerformanceRecordsModel : PageModel
    {
        private readonly AppDbContext _context;
        public PerformanceRecordsModel(AppDbContext context) => _context = context;

        public Employee Employee { get; set; } = default!;

        // Only Finalized evaluations are shown - a Draft is still a manager's
        // work in progress and hasn't been communicated to the employee yet.
        public List<Model.PerformanceEvaluation> Evaluations { get; set; } = new();

        public List<Model.Feedback> FeedbackEntries { get; set; } = new();

        public EmployeePerformanceStats Stats { get; set; } = new();

        // Performance snapshot for the shared "view full evaluation" modal -
        // scoped to each evaluation's own period rather than the all-time
        // Stats above, since the modal is about what happened during that
        // specific period. Keyed by EvaluationID.
        public Dictionary<int, EmployeePerformanceStats> EvaluationStats { get; set; } = new();

        public int EvaluationsCompleted => Evaluations.Count;

        public decimal OverallScore => Evaluations.Any()
            ? Math.Round(Evaluations.Average(e => e.OverallScore), 2, MidpointRounding.AwayFromZero)
            : 0;

        public decimal OverallStars => EvaluationScoring.StarsFor(OverallScore);

        public string OverallRating => Evaluations.Any()
            ? EvaluationScoring.RatingFor(OverallScore)
            : "Not yet evaluated";

        public Model.PerformanceEvaluation? LastEvaluation => Evaluations.FirstOrDefault();

        public List<int> Years => Evaluations
            .Select(e => e.EvaluationDate.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToList();

        // Evaluation Periods present in the history, newest first, for a more
        // precise filter than Year alone - there's at most one evaluation per
        // period, so this pins down a single row instead of a whole year's
        // worth. Evaluations is already ordered newest first, so Distinct()
        // here preserves that order.
        public List<string> Periods => Evaluations
            .Select(e => e.EvaluationPeriod)
            .Distinct()
            .ToList();

        public async Task<IActionResult> OnGetAsync()
        {
            var employeeId = HttpContext.Session.GetInt32("CurrentFieldTechnicianId");
            if (employeeId == null)
            {
                return RedirectToPage("/Login");
            }

            var employee = await _context.Employees
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId.Value);

            if (employee == null || employee.RoleType != RoleType.FieldTechnician)
            {
                HttpContext.Session.Remove("CurrentFieldTechnicianId");
                return RedirectToPage("/Login");
            }

            Employee = employee;

            Evaluations = await _context.PerformanceEvaluations
                .Include(e => e.Results).ThenInclude(r => r.Criteria)
                .Where(e => e.EmployeeID == employeeId.Value && e.EvaluationStatus == EvaluationStatus.Finalized)
                .OrderByDescending(e => e.EvaluationDate)
                .ThenByDescending(e => e.EvaluationID)
                .ToListAsync();

            FeedbackEntries = await _context.Feedbacks
                .Where(f => f.EmployeeID == employeeId.Value)
                .OrderByDescending(f => f.DateCreated)
                .Take(10)
                .ToListAsync();

            Stats = await EmployeePerformanceStatsBuilder.BuildForAsync(_context, employeeId.Value);

            foreach (var e in Evaluations)
            {
                EvaluationStats[e.EvaluationID] = await EmployeePerformanceStatsBuilder.BuildForAsync(
                    _context, employeeId.Value, e.EvaluationPeriodStart, e.EvaluationPeriodEnd);
            }

            return Page();
        }
    }
}
