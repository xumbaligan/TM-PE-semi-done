using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.OfficeStaff
{
    // Read-only "My Performance Records" for the logged-in Office Staff
    // employee: their own evaluation history. Mirrors
    // Manager/AppraisalRecords/Details, scoped to the current session's
    // employee instead of an {id} route param, and with nothing to edit -
    // an employee can only ever view their own record here.
    public class PerformanceRecordsModel : PageModel
    {
        private readonly AppDbContext _context;
        public PerformanceRecordsModel(AppDbContext context) => _context = context;

        public Employee Employee { get; set; } = default!;

        // Only Finalized evaluations are shown - a Draft is still a manager's
        // work in progress and hasn't been communicated to the employee yet.
        public List<Model.PerformanceEvaluation> Evaluations { get; set; } = new();

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
            var employeeId = HttpContext.Session.GetInt32("CurrentEmployeeId");
            if (employeeId == null)
            {
                return RedirectToPage("/Login");
            }

            var employee = await _context.Employees
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId.Value);

            if (employee == null || employee.RoleType != RoleType.OfficeStaff)
            {
                HttpContext.Session.Remove("CurrentEmployeeId");
                return RedirectToPage("/Login");
            }

            Employee = employee;

            Evaluations = await _context.PerformanceEvaluations
                .Include(e => e.Results).ThenInclude(r => r.Criteria)
                .Where(e => e.EmployeeID == employeeId.Value && e.EvaluationStatus == EvaluationStatus.Finalized)
                .OrderByDescending(e => e.EvaluationDate)
                .ThenByDescending(e => e.EvaluationID)
                .ToListAsync();

            Stats = await EmployeePerformanceStatsBuilder.BuildForAsync(_context, employeeId.Value);

            foreach (var e in Evaluations)
            {
                EvaluationStats[e.EvaluationID] = await EmployeePerformanceStatsBuilder.BuildForAsync(
                    _context, employeeId.Value, e.EvaluationPeriodStart, e.EvaluationPeriodEnd);
            }

            return Page();
        }

        // ---------------------------------------------------------------
        // Called via AJAX when the employee clicks one of the summary
        // tiles above. Mirrors Manager/PerformanceEvaluation/Create's own
        // OnGetRecordsAsync, but the employee is always read from session -
        // never a query-string/posted id - so an office staff can only ever
        // drill into their own office tasks, and the result is never
        // period-bound since Stats above is an all-time snapshot.
        //
        // metric is one of: completed, ontime, rejected, overdue -
        // matching whichever tile was clicked.
        public async Task<IActionResult> OnGetRecordsAsync(string metric)
        {
            var employeeId = HttpContext.Session.GetInt32("CurrentEmployeeId");
            if (employeeId == null)
            {
                return new JsonResult(Array.Empty<RecordItem>());
            }

            var assignments = await _context.TaskAssignments
                .Include(a => a.OfficeTask).ThenInclude(t => t!.Activities)
                .Where(a => a.EmployeeID == employeeId.Value && a.OfficeTask != null)
                .ToListAsync();

            var activityIds = assignments.SelectMany(a => a.OfficeTask!.Activities).Select(x => x.ActivityID).ToList();
            var latestSubs = await _context.ActivitySubmissions
                .Where(s => activityIds.Contains(s.ActivityID))
                .OrderByDescending(s => s.DateSubmitted)
                .GroupBy(s => s.ActivityID)
                .Select(g => g.First())
                .ToDictionaryAsync(s => s.ActivityID, s => s);

            var items = new List<RecordItem>();

            foreach (var a in assignments)
            {
                var t = a.OfficeTask!;
                bool matches = metric switch
                {
                    "completed" => string.Equals(t.Status, "Completed", StringComparison.OrdinalIgnoreCase),
                    "ontime" => string.Equals(t.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(t.Status, "Overdue", StringComparison.OrdinalIgnoreCase),
                    "rejected" => t.Activities.Any(x => x.AssignedEmployeeID == employeeId.Value && x.Status == "Rejected"),
                    "overdue" => string.Equals(t.Status, "Overdue", StringComparison.OrdinalIgnoreCase),
                    _ => false
                };
                if (!matches) continue;

                string? onTime = null;
                if (metric == "ontime")
                {
                    if (string.Equals(t.Status, "Overdue", StringComparison.OrdinalIgnoreCase))
                    {
                        onTime = "No";
                    }
                    else
                    {
                        var finishedOn = t.Activities
                            .Select(x => latestSubs.TryGetValue(x.ActivityID, out var s) ? s.DateSubmitted.Date : (DateTime?)null)
                            .Where(d => d.HasValue)
                            .Select(d => d!.Value)
                            .DefaultIfEmpty(t.DueDate.Date)
                            .Max();
                        onTime = finishedOn <= t.DueDate.Date ? "Yes" : "No";
                    }
                }

                items.Add(new RecordItem
                {
                    Type = "Office Task",
                    Number = t.TaskNumber,
                    Title = t.TaskName,
                    Status = t.DisplayStatus,
                    DateLabel = t.DueDate.ToString("M/d/yyyy"),
                    SortDate = t.DueDate,
                    OnTime = onTime,
                    Activities = t.Activities.Select(x => new ActivityDetail
                    {
                        ActivityName = x.ActivityName,
                        Status = x.Status,
                        Feedback = string.IsNullOrWhiteSpace(x.FeedBack) ? null : x.FeedBack,
                        Files = latestSubs.TryGetValue(x.ActivityID, out var sub)
                            ? new List<RecordFile> { new RecordFile { FileName = sub.FileName, FilePath = sub.FilePath } }
                            : new List<RecordFile>()
                    }).ToList()
                });
            }

            return new JsonResult(items.OrderByDescending(i => i.SortDate).ToList());
        }

        public class RecordItem
        {
            public string Type { get; set; } = string.Empty;
            public string Number { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string? DateLabel { get; set; }
            public string? OnTime { get; set; }
            public List<ActivityDetail> Activities { get; set; } = new();

            [System.Text.Json.Serialization.JsonIgnore]
            public DateTime SortDate { get; set; }
        }

        public class ActivityDetail
        {
            public string ActivityName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string? Feedback { get; set; }
            public List<RecordFile> Files { get; set; } = new();
        }

        public class RecordFile
        {
            public string FileName { get; set; } = string.Empty;
            public string FilePath { get; set; } = string.Empty;
        }
    }
}
