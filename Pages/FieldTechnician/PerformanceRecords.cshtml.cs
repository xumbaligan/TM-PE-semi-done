using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.FieldTechnician
{
    // Read-only "My Performance Records" for the logged-in Field Technician:
    // their own evaluation history. Mirrors Manager/AppraisalRecords/Details,
    // scoped to the current session's employee instead of an {id} route
    // param, and with nothing to edit - a technician can only ever view
    // their own record here.
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

            Stats = await EmployeePerformanceStatsBuilder.BuildForAsync(_context, employeeId.Value);

            foreach (var e in Evaluations)
            {
                EvaluationStats[e.EvaluationID] = await EmployeePerformanceStatsBuilder.BuildForAsync(
                    _context, employeeId.Value, e.EvaluationPeriodStart, e.EvaluationPeriodEnd);
            }

            return Page();
        }

        // ---------------------------------------------------------------
        // Called via AJAX when the technician clicks one of the summary
        // tiles above. Mirrors Manager/PerformanceEvaluation/Create's own
        // OnGetRecordsAsync, but the employee is always read from session -
        // never a query-string/posted id - so a technician can only ever
        // drill into their own job tickets, and the result is never
        // period-bound since Stats above is an all-time snapshot.
        //
        // metric is one of: completed, ontime, rescheduled, cancelled -
        // matching whichever tile was clicked.
        public async Task<IActionResult> OnGetRecordsAsync(string metric)
        {
            var employeeId = HttpContext.Session.GetInt32("CurrentFieldTechnicianId");
            if (employeeId == null)
            {
                return new JsonResult(Array.Empty<RecordItem>());
            }

            var assignments = await _context.JobTicketAssignments
                .Include(a => a.JobTicket).ThenInclude(t => t!.RescheduleHistory)
                .Where(a => a.EmployeeID == employeeId.Value && a.JobTicket != null)
                .ToListAsync();

            var ticketIds = assignments.Select(a => a.JobTicketID).ToList();

            var completionDates = await _context.JobTicketSubmissionHistories
                .Where(h => ticketIds.Contains(h.JobTicketID) && h.Status == JobTicketStatuses.Completed)
                .GroupBy(h => h.JobTicketID)
                .Select(g => new { JobTicketID = g.Key, FinishedOn = g.Max(h => h.DateChanged) })
                .ToDictionaryAsync(x => x.JobTicketID, x => x.FinishedOn);

            // Same reasoning as Manager/PerformanceEvaluation/Create's own
            // OnGetRecordsAsync: whichever History of Submission entry
            // actually recorded the terminal status change (or, for a
            // reschedule, the most recent Reschedule History entry) - not
            // every file ever uploaded across every status change.
            var terminalProofFiles = (await _context.JobTicketSubmissionHistories
                    .Include(h => h.ArchivedSubmissions)
                    .Where(h => ticketIds.Contains(h.JobTicketID)
                        && (h.Status == JobTicketStatuses.Completed || h.Status == JobTicketStatuses.Cancelled))
                    .ToListAsync())
                .GroupBy(h => (h.JobTicketID, h.Status))
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(h => h.DateChanged).First().ArchivedSubmissions);

            var latestRescheduleProofFiles = (await _context.JobTicketRescheduleHistories
                    .Include(h => h.ArchivedSubmissions)
                    .Where(h => ticketIds.Contains(h.JobTicketID))
                    .ToListAsync())
                .GroupBy(h => h.JobTicketID)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(h => h.DateChanged).First().ArchivedSubmissions);

            var items = new List<RecordItem>();

            foreach (var a in assignments)
            {
                var t = a.JobTicket!;
                bool matches = metric switch
                {
                    "completed" => t.Status == JobTicketStatuses.Completed,
                    "ontime" => t.Status == JobTicketStatuses.Completed || t.Status == JobTicketStatuses.Overdue,
                    "rescheduled" => t.Status is JobTicketStatuses.Rescheduled or JobTicketStatuses.RescheduleRequest
                        || t.RescheduleHistory.Any(),
                    "cancelled" => t.Status == JobTicketStatuses.Cancelled,
                    _ => false
                };
                if (!matches) continue;

                string? onTime = null;
                if (metric == "ontime")
                {
                    if (t.Status == JobTicketStatuses.Overdue)
                    {
                        onTime = "No";
                    }
                    else
                    {
                        var finishedOn = completionDates.TryGetValue(t.JobTicketID, out var d)
                            ? d.Date
                            : t.DateOfCompletion!.Value.Date;
                        onTime = finishedOn <= t.DateOfCompletion!.Value.Date ? "Yes" : "No";
                    }
                }

                items.Add(new RecordItem
                {
                    Type = "Job Ticket",
                    Number = t.TicketNumber,
                    Title = t.JobType,
                    Status = t.DisplayStatus,
                    DateLabel = t.DateOfCompletion?.ToString("M/d/yyyy"),
                    SortDate = t.DateOfCompletion ?? DateTime.MinValue,
                    OnTime = onTime,
                    Remarks = string.IsNullOrWhiteSpace(t.Remarks) ? null : t.Remarks,
                    Files = (metric == "rescheduled"
                            ? latestRescheduleProofFiles.TryGetValue(t.JobTicketID, out var rescheduleSubs) ? rescheduleSubs : null
                            : terminalProofFiles.TryGetValue((t.JobTicketID, t.Status), out var terminalSubs) ? terminalSubs : null)
                        is { } proofSubs
                        ? proofSubs
                            .OrderByDescending(s => s.DateSubmitted)
                            .Select(s => new RecordFile { FileName = s.FileName, FilePath = s.FilePath })
                            .ToList()
                        : new List<RecordFile>()
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
            public string? Remarks { get; set; }
            public List<RecordFile> Files { get; set; } = new();

            [System.Text.Json.Serialization.JsonIgnore]
            public DateTime SortDate { get; set; }
        }

        public class RecordFile
        {
            public string FileName { get; set; } = string.Empty;
            public string FilePath { get; set; } = string.Empty;
        }
    }
}
