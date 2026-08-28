using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.Manager.PerformanceReport
{
    // Assembles a management report entirely from records that already exist -
    // Employees, Departments, Performance Evaluations, Job Tickets and Office
    // Tasks. Nothing on this page is entered by hand and nothing is written
    // back; it's a read-only roll-up filtered by evaluation period and
    // department.
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        public IndexModel(AppDbContext context) => _context = context;

        // Admin can reach this page too (see Program.cs RBAC middleware), but
        // gets the Admin sidebar instead of the full Manager nav, since Admin's
        // access here is limited to viewing this report and exporting it.
        public string LayoutName { get; set; } = "_Layout";

        // The "Generate Report" button is a redundant explicit re-submit (the
        // filters already auto-submit via JS on change, and the report is
        // already built on the initial page load) - Admin only gets Export CSV,
        // not that generate action, so it's hidden for them.
        public bool IsAdmin { get; set; }

        // Filters arrive on the query string so "Generate Report" is a plain GET
        // - the report is bookmarkable and Export CSV can reuse the same values.
        // "yyyy-MM" - sortable and unambiguous on the wire; PeriodOptions
        // carries the human-readable label ("August 2026") for the dropdown.
        [BindProperty(SupportsGet = true)]
        public string? Period { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? DepartmentId { get; set; }

        public List<PeriodOption> PeriodOptions { get; set; } = new();
        public List<Department> DepartmentOptions { get; set; } = new();

        public string PeriodLabel => string.IsNullOrWhiteSpace(Period)
            ? "All Periods"
            : (PeriodOptions.FirstOrDefault(p => p.Value == Period)?.Label ?? Period);

        public class PeriodOption
        {
            public string Value { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;
        }

        public List<ReportRow> Rows { get; set; } = new();

        // ---- Summary tiles ----
        public int EmployeeCount => Rows.Count;
        public int AppraisalCount => Rows.Sum(r => r.Appraisals);

        // Counts distinct completed job tickets/office tasks — a job ticket
        // or task assigned to multiple employees only counts once toward
        // this tile, not once per assignee.
        public int CompletedWorkItems { get; set; }

        // Averaged over employees who actually have an evaluation in the
        // selected period, so unevaluated staff don't drag the figure to zero.
        public decimal AverageScore
        {
            get
            {
                var scored = Rows.Where(r => r.Appraisals > 0).ToList();
                return scored.Count == 0
                    ? 0
                    : Math.Round(scored.Average(r => r.AverageScore), 2, MidpointRounding.AwayFromZero);
            }
        }

        public decimal AverageStars => EvaluationScoring.StarsFor(AverageScore);

        // ---- Chart data ----
        // Bar chart: average score per employee - every employee in the
        // filtered list, not just the evaluated ones. Silently dropping
        // unevaluated staff made the chart understate headcount (4 bars for
        // a 10-person team reads as "4 employees"), and the gap itself is
        // useful information, not noise. Evaluated employees come first,
        // highest score first, so the ranking stays readable; unevaluated
        // ones follow, alphabetically, as empty gray bars.
        public List<EmployeeScorePoint> ScoreByEmployee => Rows
            .Where(r => r.Appraisals > 0)
            .OrderByDescending(r => r.AverageScore)
            .Select(r => new EmployeeScorePoint { Label = r.EmployeeName, Value = r.AverageScore, Rating = r.Rating })
            .Concat(Rows
                .Where(r => r.Appraisals == 0)
                .OrderBy(r => r.EmployeeName)
                .Select(r => new EmployeeScorePoint { Label = r.EmployeeName, Value = null, Rating = r.Rating }))
            .ToList();

        public class EmployeeScorePoint
        {
            public string Label { get; set; } = string.Empty;

            // Null for an employee with no evaluation in the selected filters.
            public decimal? Value { get; set; }

            // "Not yet evaluated" when Value is null, otherwise one of
            // EvaluationScoring's rating bands - drives the bar's color.
            public string Rating { get; set; } = string.Empty;
        }

        // Pie chart: how many employees fall into each rating band.
        public List<ChartPoint> RatingDistribution
        {
            get
            {
                var scored = Rows.Where(r => r.Appraisals > 0).ToList();
                return EvaluationScoring.Bands
                    .Select(b => new ChartPoint
                    {
                        Label = b.Rating,
                        Value = scored.Count(r => r.Rating == b.Rating)
                    })
                    .Where(p => p.Value > 0)
                    .ToList();
            }
        }

        // How many of the listed employees actually have an evaluation inside
        // the selected filters - the number that decides whether the charts
        // have anything to draw.
        public int EvaluatedEmployeeCount => Rows.Count(r => r.Appraisals > 0);

        public bool HasData => EvaluatedEmployeeCount > 0;

        public class ChartPoint
        {
            public string Label { get; set; } = string.Empty;
            public decimal Value { get; set; }
        }

        public class ReportRow
        {
            public int EmployeeId { get; set; }
            public string EmployeeName { get; set; } = string.Empty;
            public RoleType RoleType { get; set; }
            public string DepartmentName { get; set; } = "-";
            public int Appraisals { get; set; }
            public decimal AverageScore { get; set; }

            public decimal Stars => EvaluationScoring.StarsFor(AverageScore);

            public string Rating => Appraisals == 0
                ? "Not yet evaluated"
                : EvaluationScoring.RatingFor(AverageScore);

            public string RoleLabel => RoleType == RoleType.FieldTechnician
                ? "Field Technician"
                : RoleType == RoleType.OfficeStaff ? "Office Staff" : RoleType.ToString();
        }

        public async Task OnGetAsync()
        {
            IsAdmin = HttpContext.Session.GetString("AuthRoleType") == "Admin";
            LayoutName = IsAdmin ? "_Admin" : "_Layout";
            await BuildReportAsync();
        }

        // Exports exactly what the on-screen table shows, using the same
        // filters, so the file always matches the report the manager is looking
        // at rather than re-querying with different defaults.
        public async Task<IActionResult> OnGetExportCsvAsync()
        {
            await BuildReportAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Performance Report");
            csv.AppendLine($"Report Period,{Csv(PeriodLabel)}");
            csv.AppendLine($"Department,{Csv(DepartmentLabel())}");
            csv.AppendLine($"Generated,{Csv(DateTime.Now.ToString("MMMM d yyyy h:mm tt"))}");
            csv.AppendLine();
            csv.AppendLine($"Employees,{EmployeeCount}");
            csv.AppendLine($"Appraisals in Period,{AppraisalCount}");
            csv.AppendLine($"Average Score,{AverageScore.ToString("0.##")}");
            csv.AppendLine($"Completed Work Items,{CompletedWorkItems}");
            csv.AppendLine();
            csv.AppendLine("Employee Name,Role,Department,Average,Rating");

            foreach (var r in Rows)
            {
                csv.AppendLine(string.Join(",",
                    Csv(r.EmployeeName),
                    Csv(r.RoleLabel),
                    Csv(r.DepartmentName),
                    r.Appraisals == 0 ? "" : r.AverageScore.ToString("0.##"),
                    Csv(r.Rating)));
            }

            var fileName = $"performance-report-{DateTime.Now:yyyyMMdd-HHmm}.csv";

            // UTF-8 BOM so Excel opens accented names correctly.
            var bytes = Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(csv.ToString()))
                .ToArray();

            return File(bytes, "text/csv", fileName);
        }

        private string DepartmentLabel()
        {
            if (DepartmentId == null) return "All Departments";
            return DepartmentOptions.FirstOrDefault(d => d.DepartmentId == DepartmentId)?.DepartmentName
                   ?? "All Departments";
        }

        // Wraps a value for CSV: doubles any quotes and quotes the field so
        // commas in a name can't shift the columns.
        private static string Csv(string? value) =>
            "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

        private async Task BuildReportAsync()
        {
            // The report only ever lists Field Technicians and Office Staff
            // (see the employee query below), so an Admin or Manager
            // department would just be a filter option that always empties
            // the report - leave those out of the dropdown entirely.
            DepartmentOptions = await _context.Departments
                .Where(d => d.RoleType != RoleType.Admin && d.RoleType != RoleType.Manager)
                .OrderBy(d => d.DepartmentName)
                .ToListAsync();

            // Only Finalized evaluations count anywhere on this report - a
            // Draft is still a manager's work in progress and hasn't been
            // communicated to the employee yet.
            var distinctPeriods = await _context.PerformanceEvaluations
                .Where(e => e.EvaluationStatus == EvaluationStatus.Finalized)
                .Select(e => new { e.EvaluationPeriodYear, e.EvaluationPeriodMonth })
                .Distinct()
                .ToListAsync();

            PeriodOptions = distinctPeriods
                .OrderByDescending(p => p.EvaluationPeriodYear)
                .ThenByDescending(p => p.EvaluationPeriodMonth)
                .Select(p => new PeriodOption
                {
                    Value = $"{p.EvaluationPeriodYear:D4}-{p.EvaluationPeriodMonth:D2}",
                    Label = new DateTime(p.EvaluationPeriodYear, p.EvaluationPeriodMonth, 1).ToString("MMMM yyyy")
                })
                .ToList();

            // Only Field Technicians and Office Staff are evaluated, so
            // Admin/Manager accounts never appear on the report.
            var employeeQuery = _context.Employees
                .Include(e => e.Department)
                .Where(e => e.RoleType != RoleType.Admin && e.RoleType != RoleType.Manager);

            if (DepartmentId.HasValue)
                employeeQuery = employeeQuery.Where(e => e.DepartmentId == DepartmentId.Value);

            var employees = await employeeQuery
                .OrderBy(e => e.FullName)
                .ToListAsync();

            var employeeIds = employees.Select(e => e.EmployeeId).ToList();

            // Evaluations for the selected period. An empty period means the
            // report covers every period on record.
            var evaluationQuery = _context.PerformanceEvaluations
                .Where(e => employeeIds.Contains(e.EmployeeID) && e.EvaluationStatus == EvaluationStatus.Finalized);

            if (!string.IsNullOrWhiteSpace(Period))
            {
                var parts = Period.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[0], out var periodYear) && int.TryParse(parts[1], out var periodMonth))
                {
                    evaluationQuery = evaluationQuery.Where(e =>
                        e.EvaluationPeriodYear == periodYear && e.EvaluationPeriodMonth == periodMonth);
                }
            }

            var evaluationSummaries = await evaluationQuery
                .GroupBy(e => e.EmployeeID)
                .Select(g => new
                {
                    EmployeeID = g.Key,
                    Count = g.Count(),
                    Average = g.Average(x => x.OverallScore)
                })
                .ToDictionaryAsync(x => x.EmployeeID, x => x);

            // Distinct completed job tickets/office tasks among these
            // employees — a ticket or task with several assignees must only
            // count once toward the tile, not once per assignee.
            var completedTicketCount = await _context.JobTicketAssignments
                .Where(a => employeeIds.Contains(a.EmployeeID)
                    && a.JobTicket != null
                    && a.JobTicket.Status == JobTicketStatuses.Completed)
                .Select(a => a.JobTicketID)
                .Distinct()
                .CountAsync();

            var completedTaskCount = await _context.TaskAssignments
                .Where(a => employeeIds.Contains(a.EmployeeID)
                    && a.OfficeTask != null
                    && a.OfficeTask.Status == "Completed")
                .Select(a => a.OfficeTaskID)
                .Distinct()
                .CountAsync();

            CompletedWorkItems = completedTicketCount + completedTaskCount;

            // Highest average score first, so the table reads best-to-worst;
            // employees with no evaluation in the selected filters (Appraisals
            // == 0, AverageScore always 0) have nothing to rank by, so they
            // sink to the bottom, alphabetically, instead of mixing in among
            // scored employees just because their score also reads as 0.
            Rows = employees.Select(e =>
            {
                evaluationSummaries.TryGetValue(e.EmployeeId, out var summary);

                return new ReportRow
                {
                    EmployeeId = e.EmployeeId,
                    EmployeeName = e.FullName,
                    RoleType = e.RoleType,
                    DepartmentName = e.Department?.DepartmentName ?? "-",
                    Appraisals = summary?.Count ?? 0,
                    AverageScore = summary == null
                        ? 0
                        : Math.Round(summary.Average, 2, MidpointRounding.AwayFromZero)
                };
            })
            .OrderByDescending(r => r.Appraisals > 0)
            .ThenByDescending(r => r.AverageScore)
            .ThenBy(r => r.EmployeeName)
            .ToList();
        }
    }
}
