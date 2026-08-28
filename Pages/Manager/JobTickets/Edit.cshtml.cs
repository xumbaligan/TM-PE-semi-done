using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.Manager.JobTickets
{
    public class EditModel : PageModel
    {
        private readonly AppDbContext _context;

        public EditModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Model.JobTicket JobTicket { get; set; } = default!;

        // The technician the manager wants to designate as team leader. Only
        // honored while the ticket is still Pending — the assigned team itself
        // can't be changed from this page, only who among them leads.
        [BindProperty]
        public int? LeaderEmployeeId { get; set; }

        // Used by the page to detect whether the submitted date actually changed,
        // both for rendering the hidden comparison field and for re-rendering it
        // correctly if validation fails.
        public DateTime OriginalServiceDate { get; set; }

        // Manager-maintained fiber plan options (added/removed from the "Fiber
        // Plans" modal on the Create page) — loaded fresh since the list can
        // change at any time.
        public List<Model.FiberPlan> FiberPlanOptions { get; set; } = new();

        public string[] JobTypeOptions { get; set; } = JobTypes.Allowed;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var jobTicket = await _context.JobTickets
                .Include(t => t.Assignments)
                    .ThenInclude(a => a.Employee)
                .Include(t => t.AssignedByEmployee)
                .FirstOrDefaultAsync(m => m.JobTicketID == id);

            if (jobTicket == null)
            {
                return NotFound();
            }

            // Overdue is purely time-based, so re-check it every time the page is
            // opened rather than relying on whatever was last saved.
            await JobTicketOverdueChecker.RefreshAsync(_context, new[] { jobTicket });

            // Completed tickets are locked — no further edits allowed. NOTE: a
            // pending Reschedule Request does NOT lock the manager out — they're
            // the one who needs to act on it (see OnPostAsync).
            if (jobTicket.IsLockedFromEditing)
            {
                return RedirectToPage("Details", new { id });
            }

            JobTicket = jobTicket;
            OriginalServiceDate = jobTicket.ServiceDate;
            LeaderEmployeeId = jobTicket.Assignments.FirstOrDefault(a => a.IsLeader)?.EmployeeID;
            FiberPlanOptions = await _context.FiberPlans.OrderBy(f => f.FiberPlanID).ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Auto-generated / assignment-related fields are not editable from this page
            ModelState.Remove("JobTicket.TicketNumber");
            ModelState.Remove("JobTicket.Assignments");
            ModelState.Remove("JobTicket.Submissions");
            // FiberPlan/Description are conditionally required depending on JobType,
            // so their built-in validation attributes are skipped in favor of manual checks below.
            ModelState.Remove("JobTicket.FiberPlan");
            ModelState.Remove("JobTicket.Description");
            // Client Full Name / Primary / Secondary Number are only collected for
            // non-Maintenance jobs, so their built-in [Required] attributes are skipped
            // in favor of the manual, JobType-aware checks below.
            ModelState.Remove("JobTicket.ClientFullName");
            ModelState.Remove("JobTicket.PrimaryNumber");
            ModelState.Remove("JobTicket.SecondaryNumber");

            var ticket = await _context.JobTickets.FirstOrDefaultAsync(t => t.JobTicketID == JobTicket.JobTicketID);

            if (ticket == null)
            {
                return NotFound();
            }

            // Completed tickets are locked — no further edits allowed, even if
            // this page was submitted directly.
            if (ticket.IsLockedFromEditing)
            {
                return RedirectToPage("Details", new { id = ticket.JobTicketID });
            }

            // Captured before any mutations below — used both to gate the leader
            // change (allowed while Pending/Overdue or In Progress) and to know
            // whether this save is resolving a pending Reschedule Request from
            // the field technician.
            bool canReassignLeader = ticket.CanReassignLeader;
            bool wasRescheduleRequest = ticket.Status == JobTicketStatuses.RescheduleRequest;

            OriginalServiceDate = ticket.ServiceDate;

            // Changing the service date reschedules the job. This also doubles as
            // how the manager resolves a pending Reschedule Request: whether the
            // date is being changed for the first time or the manager is acting on
            // a request the field technician leader submitted, it's the same action.
            // No reason is required from the manager for this.
            bool dateChanged = JobTicket.ServiceDate.Date != ticket.ServiceDate.Date;

            // A pending Reschedule Request can only be resolved by the manager
            // actually setting a (new) service date here.
            if (wasRescheduleRequest && !dateChanged)
            {
                ModelState.AddModelError(
                    "JobTicket.ServiceDate",
                    "This job order has a pending reschedule request. Please set a new service date to resolve it.");
            }

            // Only enforced when the manager is actually rescheduling - an
            // existing ticket's ServiceDate can already be in the past (e.g.
            // still Pending days after the originally scheduled date), and
            // leaving that untouched must still be allowed to save.
            if (dateChanged && JobTicket.ServiceDate.Date < DateTime.Now.Date)
            {
                ModelState.AddModelError("JobTicket.ServiceDate", "Date cannot be rescheduled to a previous day.");
            }

            if (JobTicket.DateOfCompletion == null)
                ModelState.AddModelError("JobTicket.DateOfCompletion", "Please set a date of completion.");
            else if (JobTicket.DateOfCompletion.Value.Date < JobTicket.ServiceDate.Date)
                ModelState.AddModelError("JobTicket.DateOfCompletion", "Date of completion cannot be before the service date.");

            // The leader can only be changed while the ticket is Pending/Overdue
            // or In Progress, and only among the technicians already assigned —
            // this page can't add/remove assignees.
            if (canReassignLeader && LeaderEmployeeId.HasValue && LeaderEmployeeId.Value != 0)
            {
                var assignedIds = await _context.JobTicketAssignments
                    .Where(a => a.JobTicketID == ticket.JobTicketID)
                    .Select(a => a.EmployeeID)
                    .ToListAsync();

                if (!assignedIds.Contains(LeaderEmployeeId.Value))
                {
                    ModelState.AddModelError(string.Empty, "The leader must be one of the technicians already assigned to this job order.");
                }
            }

            if (!JobTypes.Allowed.Contains(JobTicket.JobType))
                ModelState.AddModelError("JobTicket.JobType", "Please select a valid job type.");

            if (JobTicket.JobType == JobTypes.Installation)
            {
                // A plan that was valid when this ticket was created but has since
                // been removed from the manager-maintained list is still accepted
                // here as long as the manager didn't actually change it (keeps the
                // existing value valid instead of forcing an unrelated edit to fail).
                bool unchangedFromExisting = JobTicket.FiberPlan == ticket.FiberPlan;

                if (string.IsNullOrWhiteSpace(JobTicket.FiberPlan) ||
                    (!unchangedFromExisting && !await _context.FiberPlans.AnyAsync(f => f.PlanName == JobTicket.FiberPlan)))
                    ModelState.AddModelError("JobTicket.FiberPlan", "Please select a valid fiber plan.");

                JobTicket.Description = null;
            }
            else if (JobTicket.JobType == JobTypes.Repair || JobTicket.JobType == JobTypes.Maintenance || JobTicket.JobType == JobTypes.Inspection)
            {
                if (string.IsNullOrWhiteSpace(JobTicket.Description))
                    ModelState.AddModelError("JobTicket.Description", "Please provide a description.");

                JobTicket.FiberPlan = null;
            }

            // Maintenance jobs don't collect client contact info — clear whatever was
            // submitted instead of requiring it. Every other job type still requires
            // Client Full Name and Primary Number.
            if (JobTicket.JobType == JobTypes.Maintenance)
            {
                JobTicket.ClientFullName = string.Empty;
                JobTicket.PrimaryNumber = string.Empty;
                JobTicket.SecondaryNumber = null;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(JobTicket.ClientFullName))
                    ModelState.AddModelError("JobTicket.ClientFullName", "Client full name is required.");

                if (string.IsNullOrWhiteSpace(JobTicket.PrimaryNumber))
                    ModelState.AddModelError("JobTicket.PrimaryNumber", "Primary number is required.");
                else if (!System.Text.RegularExpressions.Regex.IsMatch(JobTicket.PrimaryNumber, @"^09\d{9}$"))
                    ModelState.AddModelError("JobTicket.PrimaryNumber", "Enter a valid 11-digit Philippine mobile number (e.g. 09171234567).");

                if (!string.IsNullOrWhiteSpace(JobTicket.SecondaryNumber) &&
                    !System.Text.RegularExpressions.Regex.IsMatch(JobTicket.SecondaryNumber, @"^09\d{9}$"))
                    ModelState.AddModelError("JobTicket.SecondaryNumber", "Enter a valid 11-digit Philippine mobile number (e.g. 09171234567).");
            }

            if (!ModelState.IsValid)
            {
                var reload = await _context.JobTickets
                    .Include(t => t.Assignments).ThenInclude(a => a.Employee)
                    .Include(t => t.AssignedByEmployee)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.JobTicketID == JobTicket.JobTicketID);

                if (reload != null)
                {
                    JobTicket.Assignments = reload.Assignments;
                    JobTicket.TicketNumber = reload.TicketNumber;
                    JobTicket.DateCreated = reload.DateCreated;
                    JobTicket.Status = reload.Status;
                    JobTicket.AssignedByEmployee = reload.AssignedByEmployee;
                    OriginalServiceDate = reload.ServiceDate;
                }

                FiberPlanOptions = await _context.FiberPlans.OrderBy(f => f.FiberPlanID).ToListAsync();

                return Page();
            }

            // If the manager changed the date — whether as a direct reschedule or
            // to resolve a pending Reschedule Request from the field technician
            // leader — log it and archive whatever remarks/photos this cycle
            // already had, then reset the ticket to Pending so the leader starts
            // fresh on the new date. Archiving happens into BOTH a Reschedule
            // History entry (manager-facing) and a Submission History entry, so
            // this reschedule also shows up in the field technician leader's own
            // "History of Submission" list.
            if (dateChanged)
            {
                var currentSubmissions = await _context.JobTicketSubmissions
                    .Where(s => s.JobTicketID == ticket.JobTicketID
                        && s.RescheduleHistoryID == null
                        && s.SubmissionHistoryID == null)
                    .ToListAsync();

                var rescheduleHistory = new Model.JobTicketRescheduleHistory
                {
                    JobTicketID = ticket.JobTicketID,
                    OldServiceDate = ticket.ServiceDate,
                    NewServiceDate = JobTicket.ServiceDate,
                    // Managers aren't required to give a reason when they reschedule
                    // (unlike a field technician's own Reschedule Request). The
                    // column itself is NOT NULL, so this stays an empty string
                    // rather than null.
                    Reason = string.Empty,
                    PreviousStatus = ticket.Status,
                    PreviousRemarks = ticket.Remarks,
                    DateChanged = DateTime.Now
                };

                var submissionHistory = new Model.JobTicketSubmissionHistory
                {
                    JobTicketID = ticket.JobTicketID,
                    // "Rescheduled by Manager" (22 chars) doesn't fit the Status
                    // column's 20-char limit and gets truncated by SQL Server,
                    // which throws instead of silently cutting it off. Use the
                    // same "Rescheduled" value the ticket's own Status is set to
                    // below, which is both accurate and within the limit.
                    Status = JobTicketStatuses.Rescheduled,
                    Remarks = null,
                    DateChanged = DateTime.Now,
                    ActorEmployeeID = HttpContext.Session.GetInt32("AuthEmployeeId")
                };

                foreach (var sub in currentSubmissions)
                {
                    rescheduleHistory.ArchivedSubmissions.Add(sub);
                    submissionHistory.ArchivedSubmissions.Add(sub);
                }

                _context.JobTicketRescheduleHistories.Add(rescheduleHistory);
                _context.JobTicketSubmissionHistories.Add(submissionHistory);
                await _context.SaveChangesAsync();

                foreach (var sub in currentSubmissions)
                {
                    sub.RescheduleHistoryID = rescheduleHistory.JobTicketRescheduleHistoryID;
                    sub.SubmissionHistoryID = submissionHistory.JobTicketSubmissionHistoryID;
                }

                // Pending gives the leader a clean slate on the new date, whether
                // this was a direct reschedule or resolving their own request. The
                // Reschedule/Submission history entries above already preserve the
                // record of what happened — the ticket itself just goes back to
                // needing to be picked up again, same as any other Pending job.
                ticket.Status = JobTicketStatuses.Pending;
                ticket.Remarks = null;
            }

            // The leader can only be re-designated while the ticket was Pending/
            // Overdue or In Progress — the assigned team itself is still fixed
            // and can't be changed here.
            if (canReassignLeader && LeaderEmployeeId.HasValue && LeaderEmployeeId.Value != 0)
            {
                var assignments = await _context.JobTicketAssignments
                    .Where(a => a.JobTicketID == ticket.JobTicketID)
                    .ToListAsync();

                if (assignments.Any(a => a.EmployeeID == LeaderEmployeeId.Value))
                {
                    foreach (var a in assignments)
                    {
                        a.IsLeader = a.EmployeeID == LeaderEmployeeId.Value;
                    }
                }
            }

            // Assignees themselves are intentionally locked after creation — only
            // who leads them (above) and the ticket's own details can change here.
            // Status is never copied from the posted JobTicket object (it isn't
            // collected on this form) — it only changes via the reschedule logic
            // above, or elsewhere (the field technician workflow, or the manager's
            // Close action).
            ticket.JobType = JobTicket.JobType;
            ticket.ClientFullName = JobTicket.ClientFullName;
            ticket.PrimaryNumber = JobTicket.PrimaryNumber;
            ticket.SecondaryNumber = JobTicket.SecondaryNumber;
            ticket.FiberPlan = JobTicket.FiberPlan;
            ticket.Description = JobTicket.Description;
            ticket.ServiceDate = JobTicket.ServiceDate;
            ticket.DateOfCompletion = JobTicket.DateOfCompletion;
            ticket.LocationAddress = JobTicket.LocationAddress;
            ticket.NearestLandmark = JobTicket.NearestLandmark;

            await _context.SaveChangesAsync();

            return RedirectToPage("Index");
        }
    }
}
