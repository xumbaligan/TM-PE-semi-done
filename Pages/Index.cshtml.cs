using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    // Admin sees the exact same dashboard as Manager (see Program.cs RBAC
    // middleware, which now lets Admin into /Manager/JobTickets,
    // /Manager/OfficeTask, /Manager/PerformanceEvaluation, and
    // /Manager/WorkLoadMonitoring alongside /Manager/PerformanceReport) -
    // only the sidebar chrome differs, since Admin still manages employees/
    // departments/criteria through its own /Admin pages instead of the
    // Manager ones.
    public string LayoutName { get; set; } = "_Layout";
    public bool IsManager { get; set; }

    // ---- 1. Period selector (This Month by default) ----
    [BindProperty(SupportsGet = true)] public string? PeriodType { get; set; }
    [BindProperty(SupportsGet = true)] public int? Month { get; set; }
    [BindProperty(SupportsGet = true)] public int? Quarter { get; set; }
    [BindProperty(SupportsGet = true)] public int? Year { get; set; }

    public int SelectedYear { get; set; }
    public int SelectedMonth { get; set; }
    public int SelectedQuarter { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public List<int> YearOptions { get; set; } = new();

    // ---- 2. Needs your attention ----
    public List<AttentionItem> AttentionItems { get; set; } = new();

    // ---- 3. Work status this period (counts only - breakdowns live in each module) ----
    public int JobTicketsThisPeriod { get; set; }
    public int OfficeTasksThisPeriod { get; set; }

    // ---- 4. Workload distribution ----
    public List<WorkloadBarItem> WorkloadDistribution { get; set; } = new();

    // ---- 5. Performance snapshot, with its denominator ----
    public decimal AverageScoreThisPeriod { get; set; }
    public int EvaluatedThisPeriod { get; set; }
    public int EligibleThisPeriod { get; set; }
    public int CoveragePercent { get; set; }
    public PerformerSnapshot? HighestPerformer { get; set; }
    public PerformerSnapshot? LowestPerformer { get; set; }

    // ---- 6. Score trend across periods ----
    public List<ChartPoint> ScoreTrend { get; set; } = new();
    public bool HasFinalizedEvaluations { get; set; }

    public async Task OnGetAsync()
    {
        IsManager = HttpContext.Session.GetString("AuthRoleType") == "Manager";
        LayoutName = IsManager ? "_Layout" : "_Admin";

        ResolvePeriod();

        await BuildNeedsAttentionAsync();
        await BuildWorkStatusAsync();
        await BuildWorkloadDistributionAsync();
        await BuildPerformanceSnapshotAsync();
        await BuildScoreTrendAsync();
    }

    // Defaults to the current month the first time the page loads (no query
    // string yet). Switching to "quarter" keeps whichever Year is selected.
    private void ResolvePeriod()
    {
        var now = DateTime.Now;
        PeriodType = PeriodType == "quarter" ? "quarter" : "month";
        SelectedYear = Year ?? now.Year;

        YearOptions = Enumerable.Range(now.Year - 4, 5).Append(SelectedYear).Distinct()
            .OrderByDescending(y => y).ToList();

        if (PeriodType == "quarter")
        {
            SelectedQuarter = Quarter is >= 1 and <= 4 ? Quarter.Value : ((now.Month - 1) / 3) + 1;
            SelectedMonth = now.Month;
            var startMonth = ((SelectedQuarter - 1) * 3) + 1;
            PeriodStart = new DateTime(SelectedYear, startMonth, 1);
            PeriodEnd = PeriodStart.AddMonths(3).AddDays(-1);
            PeriodLabel = $"Q{SelectedQuarter} {SelectedYear}";
        }
        else
        {
            SelectedMonth = Month is >= 1 and <= 12 ? Month.Value : now.Month;
            SelectedQuarter = ((SelectedMonth - 1) / 3) + 1;
            PeriodStart = new DateTime(SelectedYear, SelectedMonth, 1);
            PeriodEnd = PeriodStart.AddMonths(1).AddDays(-1);
            PeriodLabel = PeriodStart.ToString("MMMM yyyy");
        }
    }

    // The calendar months (1-12) that make up the currently selected period -
    // just the one month in "month" mode, or all three months of the quarter
    // in "quarter" mode. Evaluations are always keyed by a single Month/Year
    // (see PerformanceEvaluation.EvaluationPeriodMonth), so this is how a
    // quarterly view is derived from that without a separate quarter concept
    // in the data model.
    private int[] PeriodMonthNumbers() =>
        PeriodType == "quarter"
            ? new[] { ((SelectedQuarter - 1) * 3) + 1, ((SelectedQuarter - 1) * 3) + 2, ((SelectedQuarter - 1) * 3) + 3 }
            : new[] { SelectedMonth };

    private async Task BuildNeedsAttentionAsync()
    {
        var periodMonths = PeriodMonthNumbers();

        var pendingReschedule = await _db.JobTickets
            .CountAsync(t => t.Status == JobTicketStatuses.RescheduleRequest);

        var awaitingReview = await _db.OfficeTasks
            .CountAsync(t => t.Activities.Any(a => a.Status == "Submitted"));

        var rejectedStalled = await _db.OfficeTasks
            .CountAsync(t => t.Activities.Any(a => a.Status == "Rejected"));

        var completedThisPeriod = await _db.JobTickets
            .CountAsync(t => t.Status == JobTicketStatuses.Completed
                && t.DateCompleted.HasValue
                && t.DateCompleted.Value.Date >= PeriodStart.Date
                && t.DateCompleted.Value.Date <= PeriodEnd.Date);

        var draftEvaluations = await _db.PerformanceEvaluations
            .CountAsync(e => e.EvaluationStatus == EvaluationStatus.Draft);

        var eligibleEmployeeIds = await _db.Employees
            .Where(e => e.IsActive && (e.RoleType == RoleType.FieldTechnician || e.RoleType == RoleType.OfficeStaff))
            .Select(e => e.EmployeeId)
            .ToListAsync();

        var evaluatedThisPeriodIds = await _db.PerformanceEvaluations
            .Where(e => e.EvaluationPeriodYear == SelectedYear && periodMonths.Contains(e.EvaluationPeriodMonth))
            .Select(e => e.EmployeeID)
            .Distinct()
            .ToListAsync();

        var notYetEvaluatedCount = eligibleEmployeeIds.Count(id => !evaluatedThisPeriodIds.Contains(id));

        var now = DateTime.Now;
        var linkMonth = periodMonths.Contains(now.Month) && SelectedYear == now.Year ? now.Month : periodMonths[0];

        AttentionItems = new List<AttentionItem>
        {
            new()
            {
                Label = "Pending reschedule requests",
                Count = pendingReschedule,
                Href = $"/Manager/JobTickets?status={Uri.EscapeDataString(JobTicketStatuses.RescheduleRequest)}",
                Icon = "bi-arrow-repeat",
                Kind = "warning"
            },
            new()
            {
                Label = "Activities awaiting review",
                Count = awaitingReview,
                Href = "/Manager/OfficeTask?activity=pending-review",
                Icon = "bi-hourglass-split",
                Kind = "warning"
            },
            new()
            {
                Label = "Completed tickets this period",
                Count = completedThisPeriod,
                Href = $"/Manager/JobTickets?status={Uri.EscapeDataString(JobTicketStatuses.Completed)}",
                Icon = "bi-check2-circle",
                Kind = "info"
            },
            new()
            {
                Label = "Activities rejected and stalled",
                Count = rejectedStalled,
                Href = "/Manager/OfficeTask?activity=rejected",
                Icon = "bi-x-octagon",
                Kind = "warning"
            },
            new()
            {
                Label = "Draft evaluations",
                Count = draftEvaluations,
                Href = "/Manager/PerformanceEvaluation?StatusFilter=Draft",
                Icon = "bi-file-earmark-text",
                Kind = "warning"
            },
            new()
            {
                Label = "Employees not yet evaluated this period",
                Count = notYetEvaluatedCount,
                Href = $"/Manager/PerformanceEvaluation/Create?Month={linkMonth}&Year={SelectedYear}",
                Icon = "bi-person-exclamation",
                Kind = "warning"
            }
        };
    }

    private async Task BuildWorkStatusAsync()
    {
        JobTicketsThisPeriod = await _db.JobTickets
            .CountAsync(t => t.ServiceDate.Date >= PeriodStart.Date && t.ServiceDate.Date <= PeriodEnd.Date);

        OfficeTasksThisPeriod = await _db.OfficeTasks
            .CountAsync(t => t.DueDate.Date >= PeriodStart.Date && t.DueDate.Date <= PeriodEnd.Date);
    }

    // Combines both workforces into one ranked list using the exact same
    // per-role point formulas as Manager > Workload Monitoring, so a
    // "Heavy"/"Idle" band here means the same thing it means there. Idle
    // reuses that page's "Light" threshold - flagged here instead of shown as
    // a quiet success color, since underutilization is exactly what this
    // chart exists to surface.
    private async Task BuildWorkloadDistributionAsync()
    {
        var officeTasks = await _db.OfficeTasks
            .Include(t => t.Assignments)
            .Include(t => t.Activities)
            .ToListAsync();

        await RefreshOverdueOfficeTaskStatusesAsync(officeTasks);

        var jobTickets = await _db.JobTickets
            .Include(t => t.Assignments)
            .ToListAsync();

        var employees = await _db.Employees
            .Where(e => e.IsActive && (e.RoleType == RoleType.FieldTechnician || e.RoleType == RoleType.OfficeStaff))
            .ToListAsync();

        var today = DateTime.Now.Date;
        var items = new List<WorkloadBarItem>();

        foreach (var emp in employees)
        {
            int points;

            if (emp.RoleType == RoleType.OfficeStaff)
            {
                var assignedTasks = officeTasks.Where(t => t.Assignments.Any(a => a.EmployeeID == emp.EmployeeId)).ToList();
                var activeTasks = assignedTasks.Count(t => t.Status is "Pending" or "In Progress" or "Overdue");
                var overdueTasks = assignedTasks.Count(t => t.Status == "Overdue");
                var pendingActivities = officeTasks
                    .SelectMany(t => t.Activities)
                    .Count(a => a.AssignedEmployeeID == emp.EmployeeId && a.Status != "Approved");

                points = (activeTasks * 2) + pendingActivities + (overdueTasks * 2);
            }
            else
            {
                var assignedTickets = jobTickets.Where(t => t.Assignments.Any(a => a.EmployeeID == emp.EmployeeId)).ToList();
                var activeTickets = assignedTickets.Count(t => t.Status is JobTicketStatuses.Pending or JobTicketStatuses.InProgress);
                var overdueTickets = assignedTickets.Count(t =>
                    t.Status is JobTicketStatuses.Pending or JobTicketStatuses.InProgress
                    && t.ServiceDate.Date < today);

                points = (activeTickets * 2) + (overdueTickets * 2);
            }

            var band = points switch
            {
                <= 2 => "Idle",
                <= 6 => "Normal",
                _ => "Heavy"
            };

            items.Add(new WorkloadBarItem
            {
                Name = emp.FullName,
                Role = emp.RoleType == RoleType.OfficeStaff ? "Office Staff" : "Field Technician",
                Points = points,
                Band = band
            });
        }

        WorkloadDistribution = items.OrderByDescending(i => i.Points).ToList();
    }

    private async Task RefreshOverdueOfficeTaskStatusesAsync(List<Model.OfficeTask> tasks)
    {
        var today = DateTime.Now.Date;
        bool changed = false;

        foreach (var task in tasks)
        {
            if (task.Status != "Completed" && task.DueDate.Date < today)
            {
                if (task.Status != "Overdue")
                {
                    task.Status = "Overdue";
                    changed = true;
                }
            }
            else if (task.Status == "Overdue" && task.DueDate.Date >= today)
            {
                task.Status = "Pending";
                changed = true;
            }
        }

        if (changed)
        {
            await _db.SaveChangesAsync();
        }
    }

    private async Task BuildPerformanceSnapshotAsync()
    {
        var periodMonths = PeriodMonthNumbers();

        EligibleThisPeriod = await _db.Employees
            .CountAsync(e => e.IsActive && (e.RoleType == RoleType.FieldTechnician || e.RoleType == RoleType.OfficeStaff));

        var finalizedThisPeriod = await _db.PerformanceEvaluations
            .Include(e => e.Employee)
            .Where(e => e.EvaluationStatus == EvaluationStatus.Finalized
                && e.EvaluationPeriodYear == SelectedYear
                && periodMonths.Contains(e.EvaluationPeriodMonth)
                && e.Employee != null)
            .ToListAsync();

        EvaluatedThisPeriod = finalizedThisPeriod.Select(e => e.EmployeeID).Distinct().Count();
        AverageScoreThisPeriod = finalizedThisPeriod.Any()
            ? Math.Round(finalizedThisPeriod.Average(e => e.OverallScore), 2)
            : 0;
        CoveragePercent = EligibleThisPeriod == 0
            ? 0
            : (int)Math.Round(EvaluatedThisPeriod * 100m / EligibleThisPeriod);

        // One row per employee (an employee can carry more than one
        // evaluation in a quarter's three months), so "highest"/"lowest"
        // names an employee, not just whichever single evaluation happened
        // to score highest.
        var perEmployee = finalizedThisPeriod
            .GroupBy(e => e.EmployeeID)
            .Select(g => new PerformerSnapshot
            {
                Name = g.First().Employee!.FullName,
                Score = Math.Round(g.Average(e => e.OverallScore), 2),
                Rating = g.OrderByDescending(e => e.EvaluationDate).First().OverallRating
            })
            .ToList();

        if (perEmployee.Any())
        {
            HighestPerformer = perEmployee.OrderByDescending(e => e.Score).First();

            // Only meaningful with at least two evaluated employees - with
            // just one, "lowest" would just be relabeling the same person
            // already shown as "highest".
            if (perEmployee.Count > 1)
            {
                LowestPerformer = perEmployee.OrderBy(e => e.Score).First();
            }
        }
    }

    // Historical trend independent of any single period's filter scope - the
    // one thing no other screen shows (Performance Reports is single-period
    // only). Granularity follows the period selector: 12 trailing months, or
    // 8 trailing quarters.
    private async Task BuildScoreTrendAsync()
    {
        var finalized = await _db.PerformanceEvaluations
            .Where(e => e.EvaluationStatus == EvaluationStatus.Finalized)
            .Select(e => new { e.EvaluationPeriodMonth, e.EvaluationPeriodYear, e.OverallScore })
            .ToListAsync();

        HasFinalizedEvaluations = finalized.Any();

        if (PeriodType == "quarter")
        {
            var currentAbsoluteQuarter = (SelectedYear * 4) + (SelectedQuarter - 1);

            ScoreTrend = Enumerable.Range(0, 8)
                .Select(i =>
                {
                    var absoluteQuarter = currentAbsoluteQuarter - (7 - i);
                    var y = absoluteQuarter / 4;
                    var q = (absoluteQuarter % 4) + 1;
                    return (Year: y, Quarter: q);
                })
                .Select(p =>
                {
                    var months = new[] { ((p.Quarter - 1) * 3) + 1, ((p.Quarter - 1) * 3) + 2, ((p.Quarter - 1) * 3) + 3 };
                    var scores = finalized
                        .Where(e => e.EvaluationPeriodYear == p.Year && months.Contains(e.EvaluationPeriodMonth))
                        .Select(e => e.OverallScore)
                        .ToList();

                    return new ChartPoint
                    {
                        Label = $"Q{p.Quarter} {p.Year}",
                        Value = scores.Any() ? Math.Round(scores.Average(), 2) : 0
                    };
                })
                .ToList();
        }
        else
        {
            var anchor = new DateTime(SelectedYear, SelectedMonth, 1);

            ScoreTrend = Enumerable.Range(0, 12)
                .Select(i => anchor.AddMonths(-(11 - i)))
                .Select(m =>
                {
                    var scores = finalized
                        .Where(e => e.EvaluationPeriodYear == m.Year && e.EvaluationPeriodMonth == m.Month)
                        .Select(e => e.OverallScore)
                        .ToList();

                    return new ChartPoint
                    {
                        Label = m.ToString("MMM yyyy"),
                        Value = scores.Any() ? Math.Round(scores.Average(), 2) : 0
                    };
                })
                .ToList();
        }
    }

    public class ChartPoint
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
    }

    public class AttentionItem
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
        public string Href { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        // "warning" = flagged red when Count > 0, shown as an all-clear when
        // zero. "info" = neutral/informational regardless of Count (e.g.
        // completed-this-period is good news, not an alert).
        public string Kind { get; set; } = "warning";
    }

    public class WorkloadBarItem
    {
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int Points { get; set; }
        public string Band { get; set; } = "Normal";
    }

    public class PerformerSnapshot
    {
        public string Name { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public string Rating { get; set; } = string.Empty;
    }
}
