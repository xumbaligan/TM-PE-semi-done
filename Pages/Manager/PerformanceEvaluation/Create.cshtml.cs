using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.Manager.PerformanceEvaluation
{
    // One Evaluation Date doubles as the appraisal date, one Status doubles as
    // the appraisal status, and the manager rates each criterion with stars
    // rather than typing raw points.
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;
        public CreateModel(AppDbContext context) => _context = context;

        [BindProperty]
        public Model.PerformanceEvaluation Evaluation { get; set; } = new()
        {
            EvaluationDate = DateTime.Now,
            EvaluationStatus = EvaluationStatus.Draft
        };

        // Posted using the "Results.Index" hidden-input form of collection
        // binding rather than plain 0..n indices, because only the criteria
        // block for the selected role type is enabled - plain indices would
        // break the moment the posted ones weren't contiguous from zero.
        [BindProperty]
        public List<ResultInput> Results { get; set; } = new();

        // Which period's stats to show, chosen via a plain query-string GET
        // reload (see the Month/Year <select>s in Create.cshtml) rather than
        // an AJAX call - simplest way to recompute the period-scoped stats
        // below without a whole client-side data layer. Defaults to the
        // current month/year when absent (a fresh page load).
        [BindProperty(SupportsGet = true)]
        public int? Month { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? Year { get; set; }

        // Carries the employee the manager had already picked through the
        // Month/Year reload above, so changing the period doesn't dump them
        // back to "Select an employee" (see Create.cshtml's reloadForPeriod).
        [BindProperty(SupportsGet = true)]
        public int? EmployeeId { get; set; }

        // Which "Select Employee Role" button is active - "FieldTechnician" or
        // "OfficeStaff". Bound both ways: as a query string param (carried
        // through the Month/Year reload and the post-save redirect back to
        // this same page) and as a posted hidden form field (see
        // Create.cshtml's setActiveRole), so the role stays put across a
        // period change or after saving, even though - unlike EmployeeId -
        // there's no server-side record of it to derive it from once the
        // employee dropdown is cleared.
        [BindProperty(SupportsGet = true)]
        public string? SelectedRole { get; set; }

        // Shown after a save so the manager gets confirmation without leaving
        // this page - see SaveAsync, which redirects back here (not to
        // Details) precisely so evaluations can be entered back-to-back.
        [TempData]
        public string? SuccessMessage { get; set; }

        // A handful of years around today for the Period Year dropdown.
        public List<int> PeriodYearOptions { get; set; } = new();

        // Only Field Technicians / Office Staff can be evaluated - Admin and
        // Manager accounts are excluded from the picker.
        public List<Employee> EmployeeList { get; set; } = new();

        // Criteria are pulled straight from the database (Manager > Criteria),
        // never hardcoded here - each RoleType gets its own weighted set.
        public List<TM_PE.Model.Criteria> FieldTechnicianCriteria { get; set; } = new();
        public List<TM_PE.Model.Criteria> OfficeStaffCriteria { get; set; } = new();

        // Completed / on-time / rescheduled / cancelled counts shown at the top
        // of the page, so the manager can see actual performance while rating.
        public Dictionary<int, EmployeePerformanceStats> Stats { get; set; } = new();

        public string CurrentManagerName { get; set; } = "Manager";

        // Merges the criteria list with anything already posted, so a
        // validation failure doesn't wipe out the stars/feedback already
        // entered.
        public List<CriteriaStarRow> RowsFor(List<TM_PE.Model.Criteria> criteria) =>
            criteria.Select(c =>
            {
                var posted = Results.FirstOrDefault(r => r.CriteriaID == c.CriteriaId);
                return new CriteriaStarRow
                {
                    Criteria = c,
                    StarRating = posted?.StarRating ?? 0,
                    Feedback = posted?.Feedback
                };
            }).ToList();

        public class ResultInput
        {
            public int CriteriaID { get; set; }
            public decimal StarRating { get; set; }
            public string? Feedback { get; set; }
        }

        public async Task OnGetAsync()
        {
            Evaluation.EvaluationPeriodMonth = Month ?? DateTime.Now.Month;
            Evaluation.EvaluationPeriodYear = Year ?? DateTime.Now.Year;
            Evaluation.EmployeeID = EmployeeId ?? 0;
            await LoadReferenceDataAsync();
        }

        public Task<IActionResult> OnPostSaveDraftAsync() => SaveAsync(EvaluationStatus.Draft);

        public Task<IActionResult> OnPostFinalizeAsync() => SaveAsync(EvaluationStatus.Finalized);

        private async Task<IActionResult> SaveAsync(EvaluationStatus status)
        {
            var employee = await _context.Employees.FindAsync(Evaluation.EmployeeID);
            if (employee == null || employee.RoleType is RoleType.Admin or RoleType.Manager)
            {
                ModelState.AddModelError("Evaluation.EmployeeID", "Please select a valid employee.");
                employee = null;
            }

            ModelState.Remove("Evaluation.Employee");
            ModelState.Remove("Evaluation.OverallScore");
            ModelState.Remove("Evaluation.OverallRating");
            ModelState.Remove("Evaluation.EvaluatorName");
            ModelState.Remove("Evaluation.EvaluationStatus");

            if (!ModelState.IsValid || employee == null)
            {
                await LoadReferenceDataAsync();
                return Page();
            }

            // One evaluation per employee per period - now that the period is a
            // real Month/Year instead of typed free text, this can be checked
            // exactly instead of relying on the manager never mistyping it.
            bool alreadyEvaluated = await _context.PerformanceEvaluations.AnyAsync(e =>
                e.EmployeeID == employee.EmployeeId &&
                e.EvaluationPeriodMonth == Evaluation.EvaluationPeriodMonth &&
                e.EvaluationPeriodYear == Evaluation.EvaluationPeriodYear);
            if (alreadyEvaluated)
            {
                ModelState.AddModelError(string.Empty,
                    $"{employee.FullName} already has an evaluation for {Evaluation.EvaluationPeriod}. Edit the existing one instead of creating a duplicate.");
                await LoadReferenceDataAsync();
                return Page();
            }

            // Only accept scores for criteria that actually belong to this
            // employee's role type and are still active - never trust the
            // posted criteria list blindly.
            var allowedCriteria = await _context.Criteria
                .Where(c => c.IsActive && c.RoleType == employee.RoleType)
                .ToListAsync();
            var allowedIds = allowedCriteria.ToDictionary(c => c.CriteriaId);

            if (status == EvaluationStatus.Finalized)
            {
                var workQualityError = EvaluationScoring.ValidateAllCriteriaRated(
                    allowedCriteria, Results.Select(r => (r.CriteriaID, r.StarRating, r.Feedback)));
                if (workQualityError != null)
                {
                    ModelState.AddModelError(string.Empty, workQualityError);
                    await LoadReferenceDataAsync();
                    return Page();
                }
            }

            var evaluation = new Model.PerformanceEvaluation
            {
                EmployeeID = employee.EmployeeId,
                // The evaluator is always the manager who's currently logged
                // in - never a free-text field a person could misattribute.
                EvaluatorName = GetCurrentManagerName(),
                EvaluationDate = Evaluation.EvaluationDate,
                EvaluationPeriodMonth = Evaluation.EvaluationPeriodMonth,
                EvaluationPeriodYear = Evaluation.EvaluationPeriodYear,
                GeneralFeedback = string.IsNullOrWhiteSpace(Evaluation.GeneralFeedback) ? null : Evaluation.GeneralFeedback.Trim(),
                // Status comes from which button was pressed, not a dropdown.
                EvaluationStatus = status,
                DateCreated = DateTime.Now
            };

            decimal overallScore = 0;
            foreach (var r in Results)
            {
                if (!allowedIds.TryGetValue(r.CriteriaID, out var criteria)) continue;

                // Clamp server-side: stars can only ever be 0-5, and the points
                // they translate into are capped by that criterion's weight.
                // Snap server-side: a hand-crafted POST can never store 3.7 stars.
                var stars = EvaluationScoring.NormalizeStars(r.StarRating);
                var score = EvaluationScoring.ScoreFor(stars, criteria.Weight);
                overallScore += score;

                evaluation.Results.Add(new EvaluationResult
                {
                    CriteriaID = criteria.CriteriaId,
                    StarRating = stars,
                    Score = score,
                    Feedback = string.IsNullOrWhiteSpace(r.Feedback) ? null : r.Feedback.Trim()
                });
            }

            evaluation.OverallScore = overallScore;
            evaluation.OverallRating = EvaluationScoring.RatingFor(overallScore);

            _context.PerformanceEvaluations.Add(evaluation);
            await _context.SaveChangesAsync();

            // Back to this same Create page rather than Details, so the
            // manager can evaluate the next employee right away. Only the
            // employee resets (there's no EmployeeId to carry forward, and the
            // one just evaluated is excluded from the dropdown anyway now)
            // - the period and role selection carry through via the redirect.
            SuccessMessage = status == EvaluationStatus.Finalized
                ? $"Finalized {employee.FullName}'s evaluation for {evaluation.EvaluationPeriod}."
                : $"Saved a draft evaluation for {employee.FullName} ({evaluation.EvaluationPeriod}).";

            return RedirectToPage("Create", new
            {
                Month = Evaluation.EvaluationPeriodMonth,
                Year = Evaluation.EvaluationPeriodYear,
                SelectedRole
            });
        }

        // ---------------------------------------------------------------
        // Stat tile drill-down (Employee performance snapshot)
        // ---------------------------------------------------------------
        // Called via AJAX when the manager clicks one of the four stat tiles
        // at the top of the page. Unlike Stats above (aggregate counts for
        // every employee in the dropdown, computed once up front), this
        // fetches the actual underlying job ticket / office task records for
        // just the one employee currently selected, on demand - the manager
        // only ever looks at one tile's detail at a time.
        //
        // metric is one of: completed, ontime, rescheduled, cancelled
        // (Field Technician), rejected, overdue (Office Staff) - matching
        // whichever tile was clicked, exactly as labeled client-side by
        // Create.cshtml's applyStats().
        public async Task<IActionResult> OnGetRecordsAsync(string metric)
        {
            if (!EmployeeId.HasValue || EmployeeId.Value == 0)
            {
                return new JsonResult(Array.Empty<RecordItem>());
            }

            var employeeId = EmployeeId.Value;
            var periodStart = new DateTime(Year ?? DateTime.Now.Year, Month ?? DateTime.Now.Month, 1);
            var periodEnd = periodStart.AddMonths(1).AddDays(-1);

            var items = new List<RecordItem>();

            bool wantsTickets = metric is "completed" or "ontime" or "rescheduled" or "cancelled";
            bool wantsTasks = metric is "completed" or "ontime" or "rejected" or "overdue";

            if (wantsTickets)
            {
                var assignments = await _context.JobTicketAssignments
                    .Include(a => a.JobTicket).ThenInclude(t => t!.Submissions)
                    .Include(a => a.JobTicket).ThenInclude(t => t!.RescheduleHistory)
                    .Where(a => a.EmployeeID == employeeId && a.JobTicket != null
                        && a.JobTicket!.DateOfCompletion != null
                        && a.JobTicket.DateOfCompletion.Value.Date >= periodStart.Date
                        && a.JobTicket.DateOfCompletion.Value.Date <= periodEnd.Date)
                    .ToListAsync();

                var ticketIds = assignments.Select(a => a.JobTicketID).ToList();
                var completionDates = await _context.JobTicketSubmissionHistories
                    .Where(h => ticketIds.Contains(h.JobTicketID) && h.Status == JobTicketStatuses.Completed)
                    .GroupBy(h => h.JobTicketID)
                    .Select(g => new { JobTicketID = g.Key, FinishedOn = g.Max(h => h.DateChanged) })
                    .ToDictionaryAsync(x => x.JobTicketID, x => x.FinishedOn);

                // The files that actually proved the job was done (or that it
                // was cancelled): whichever History of Submission entry
                // recorded that status change (see FieldTechnician
                // DetailsModel.OnPostSaveAsync, which archives the
                // then-current submissions under it) - not every file ever
                // uploaded across every status change. Keyed by (ticket,
                // status) since a ticket can only ever reach one of these two
                // terminal statuses, but both are looked up the same way.
                var terminalProofFiles = (await _context.JobTicketSubmissionHistories
                        .Include(h => h.ArchivedSubmissions)
                        .Where(h => ticketIds.Contains(h.JobTicketID)
                            && (h.Status == JobTicketStatuses.Completed || h.Status == JobTicketStatuses.Cancelled))
                        .ToListAsync())
                    .GroupBy(h => (h.JobTicketID, h.Status))
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderByDescending(h => h.DateChanged).First().ArchivedSubmissions);

                // Same idea for a rescheduled ticket: each reschedule archives
                // whatever proof was on the ticket at that moment under its own
                // Reschedule History entry (see Manager JobTickets EditModel and
                // FieldTechnician DetailsModel.OnPostSaveAsync) - one reschedule,
                // one proof snapshot. Use the most recent reschedule's snapshot.
                var latestRescheduleProofFiles = (await _context.JobTicketRescheduleHistories
                        .Include(h => h.ArchivedSubmissions)
                        .Where(h => ticketIds.Contains(h.JobTicketID))
                        .ToListAsync())
                    .GroupBy(h => h.JobTicketID)
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderByDescending(h => h.DateChanged).First().ArchivedSubmissions);

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
                        // Only the proof for this specific event: the
                        // Rescheduled tile uses the most recent reschedule's
                        // own snapshot; everything else uses whichever
                        // terminal status (Completed/Cancelled) this row
                        // actually is. Empty for a ticket that never reached
                        // the event this row represents (e.g. an Overdue
                        // ticket under the On-time tile).
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
            }

            if (wantsTasks)
            {
                var assignments = await _context.TaskAssignments
                    .Include(a => a.OfficeTask).ThenInclude(t => t!.Activities)
                    .Where(a => a.EmployeeID == employeeId && a.OfficeTask != null
                        && a.OfficeTask!.DueDate.Date >= periodStart.Date
                        && a.OfficeTask.DueDate.Date <= periodEnd.Date)
                    .ToListAsync();

                var activityIds = assignments.SelectMany(a => a.OfficeTask!.Activities).Select(x => x.ActivityID).ToList();
                var latestSubs = await _context.ActivitySubmissions
                    .Where(s => activityIds.Contains(s.ActivityID))
                    .OrderByDescending(s => s.DateSubmitted)
                    .GroupBy(s => s.ActivityID)
                    .Select(g => g.First())
                    .ToDictionaryAsync(s => s.ActivityID, s => s);

                foreach (var a in assignments)
                {
                    var t = a.OfficeTask!;
                    bool matches = metric switch
                    {
                        "completed" => string.Equals(t.Status, "Completed", StringComparison.OrdinalIgnoreCase),
                        "ontime" => string.Equals(t.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(t.Status, "Overdue", StringComparison.OrdinalIgnoreCase),
                        "rejected" => t.Activities.Any(x => x.AssignedEmployeeID == employeeId && x.Status == "Rejected"),
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
            public List<ActivityDetail> Activities { get; set; } = new();

            [System.Text.Json.Serialization.JsonIgnore]
            public DateTime SortDate { get; set; }
        }

        public class RecordFile
        {
            public string FileName { get; set; } = string.Empty;
            public string FilePath { get; set; } = string.Empty;
        }

        public class ActivityDetail
        {
            public string ActivityName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string? Feedback { get; set; }
            public List<RecordFile> Files { get; set; } = new();
        }

        private string GetCurrentManagerName() =>
            HttpContext.Session.GetString("AuthEmployeeName") ?? "Manager";

        private async Task LoadReferenceDataAsync()
        {
            CurrentManagerName = GetCurrentManagerName();

            // An employee who already has an evaluation for this exact period
            // can't be picked again (see SaveAsync's own duplicate check) - hide
            // them from the dropdown entirely rather than letting the manager
            // pick them and only find out on Save. Scoped to the period
            // currently being viewed, so switching Month/Year (a full page
            // reload - see reloadForPeriod in Create.cshtml) brings them right
            // back for any period they don't already have one in.
            var alreadyEvaluatedEmployeeIds = await _context.PerformanceEvaluations
                .Where(e => e.EvaluationPeriodMonth == Evaluation.EvaluationPeriodMonth
                    && e.EvaluationPeriodYear == Evaluation.EvaluationPeriodYear)
                .Select(e => e.EmployeeID)
                .ToListAsync();

            EmployeeList = await _context.Employees
                .Where(e => e.IsActive && e.RoleType != RoleType.Admin && e.RoleType != RoleType.Manager
                    && !alreadyEvaluatedEmployeeIds.Contains(e.EmployeeId))
                .OrderBy(e => e.FullName)
                .ToListAsync();

            FieldTechnicianCriteria = await _context.Criteria
                .Where(c => c.IsActive && c.RoleType == RoleType.FieldTechnician)
                .OrderByDescending(c => c.Weight)
                .ToListAsync();

            OfficeStaffCriteria = await _context.Criteria
                .Where(c => c.IsActive && c.RoleType == RoleType.OfficeStaff)
                .OrderByDescending(c => c.Weight)
                .ToListAsync();

            var currentYear = DateTime.Now.Year;
            PeriodYearOptions = Enumerable.Range(currentYear - 4, 6).Reverse().ToList();
            if (!PeriodYearOptions.Contains(Evaluation.EvaluationPeriodYear))
            {
                PeriodYearOptions.Insert(0, Evaluation.EvaluationPeriodYear);
            }

            Stats = await EmployeePerformanceStatsBuilder.BuildAsync(
                _context, EmployeeList.Select(e => e.EmployeeId),
                Evaluation.EvaluationPeriodStart, Evaluation.EvaluationPeriodEnd);
        }
    }
}
