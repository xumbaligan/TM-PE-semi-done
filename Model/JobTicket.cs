using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TM_PE.Model
{
    // Fiber subscription plans a job ticket can be created for. Managers can add/remove
    // plans at runtime (see Pages/Manager/JobTickets/Create.cshtml, "Fiber Plans" modal);
    // the actual list now lives in the tbl_fiberplan table (see the FiberPlan entity below)
    // instead of being hardcoded.
    public static class FiberPlanRules
    {
        // Letters, numbers, spaces, and a small set of punctuation commonly needed
        // for plan names/prices (₱, ., ,, -, /, #, &, parentheses).
        public const string AllowedPattern = @"^[A-Za-z0-9À-ÖØ-öø-ÿ.,\-\/₱#&()\s]+$";
        public const int MaxLength = 50;
    }

    // The kind of job a ticket covers. Drives which fields appear on the form.
    public static class JobTypes
    {
        public const string Installation = "Installation";
        public const string Repair = "Repair";
        public const string Maintenance = "Maintenance";
        public const string Inspection = "Inspection";

        public static readonly string[] Allowed = { Installation, Repair, Maintenance, Inspection };
    }

    // Job ticket lifecycle. "Completed" is the terminal state - once a ticket is
    // Completed it is locked from further edits; there is no separate manager
    // "close" step. "Reschedule Request" can be set either by the manager
    // directly (immediate reschedule) or requested by the field technician
    // leader (awaiting manager approval). "Overdue" is never chosen directly by
    // anyone — it's computed automatically (see JobTicketOverdueChecker)
    // whenever a ticket's Date of Completion has passed and the job hasn't been
    // finished yet.
    public static class JobTicketStatuses
    {
        public const string Pending = "Pending";
        public const string InProgress = "In Progress";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
        public const string RescheduleRequest = "Reschedule Request";
        public const string Rescheduled = "Rescheduled";
        public const string Overdue = "Overdue";

        // Never stored - purely a display label for a Completed ticket whose
        // DateCompleted landed after its DateOfCompletion deadline (see
        // JobTicket.IsCompletedLate/DisplayStatus below). Status itself stays
        // "Completed" so every existing Completed check (locking, dashboards,
        // performance stats) keeps working unchanged.
        public const string CompletedLate = "Completed Late";

        public static readonly string[] Allowed = { Pending, InProgress, Completed, Cancelled, RescheduleRequest };
    }

    // A ticket becomes Overdue purely because time has passed, not because
    // anyone edited it — so this is re-checked on every page load (wherever a
    // ticket is displayed) rather than stored as a one-time decision. Only
    // Pending and In Progress tickets are eligible: those are the two states
    // representing work that hasn't been finished or otherwise resolved yet.
    public static class JobTicketOverdueChecker
    {
        public static bool IsOverdue(JobTicket ticket) =>
            (ticket.Status == JobTicketStatuses.Pending || ticket.Status == JobTicketStatuses.InProgress)
            && ticket.DateOfCompletion.HasValue
            && ticket.DateOfCompletion.Value.Date < DateTime.Now.Date;

        // Recomputes Overdue for every ticket in the list and persists any
        // changes. Also reverts a ticket that's marked Overdue back to Pending
        // if its Date of Completion was pushed out again (e.g. via Edit) so it
        // no longer qualifies.
        public static async Task RefreshAsync(TM_PE.Data.AppDbContext context, IEnumerable<JobTicket> tickets)
        {
            var today = DateTime.Now.Date;
            bool changed = false;

            foreach (var ticket in tickets)
            {
                if (IsOverdue(ticket))
                {
                    if (ticket.Status != JobTicketStatuses.Overdue)
                    {
                        ticket.Status = JobTicketStatuses.Overdue;
                        changed = true;
                    }
                }
                else if (ticket.Status == JobTicketStatuses.Overdue
                    && (!ticket.DateOfCompletion.HasValue || ticket.DateOfCompletion.Value.Date >= today))
                {
                    ticket.Status = JobTicketStatuses.Pending;
                    changed = true;
                }
            }

            if (changed)
            {
                await context.SaveChangesAsync();
            }
        }
    }

    [Table("tbl_jobticket")]
    public class JobTicket
    {
        [Key]
        public int JobTicketID { get; set; }

        [Required]
        [StringLength(20)]
        public string TicketNumber { get; set; } = string.Empty;

        public DateTime DateCreated { get; set; } = DateTime.Now;

        // One of JobTypes.Allowed (Installation / Repair / Maintenance)
        [Required(ErrorMessage = "Job type is required.")]
        [StringLength(20)]
        public string JobType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Client full name is required.")]
        [StringLength(100)]
        public string ClientFullName { get; set; } = string.Empty;

        // Philippine mobile format: exactly 11 digits, starting with 09 (e.g.
        // 09171234567). StringLength stays 20 (matches the existing DB
        // column) so tightening this format doesn't require a migration -
        // the RegularExpression is what actually enforces the 11-digit rule.
        [Required(ErrorMessage = "Primary number is required.")]
        [StringLength(20)]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "Enter a valid 11-digit Philippine mobile number (e.g. 09171234567).")]
        public string PrimaryNumber { get; set; } = string.Empty;

        [StringLength(20)]
        [RegularExpression(@"^09\d{9}$", ErrorMessage = "Enter a valid 11-digit Philippine mobile number (e.g. 09171234567).")]
        public string? SecondaryNumber { get; set; }

        // Only used when JobType == Installation. Free-text plan name/price chosen
        // from the manager-maintained tbl_fiberplan list (see FiberPlan entity below).
        [StringLength(FiberPlanRules.MaxLength)]
        public string? FiberPlan { get; set; }

        // Only used when JobType == Repair or Maintenance.
        [StringLength(500)]
        public string? Description { get; set; }

        // Meaning depends on JobType: install date, repair date, or maintenance date.
        [Required(ErrorMessage = "Date is required.")]
        public DateTime ServiceDate { get; set; } = DateTime.Now;

        // Despite the name, this is the DEADLINE the manager sets for the job to
        // be finished by - shown to field technicians as "Due Date" - not the
        // date it was actually finished (see DateCompleted below for that). Not
        // required at the database level (existing tickets predate this field),
        // but the Create page requires it — see CreateModel.OnPostAsync. Drives
        // the automatic Overdue status (see JobTicketOverdueChecker) once it
        // passes and the job still isn't done.
        public DateTime? DateOfCompletion { get; set; }

        // The date the field technician leader actually marked this job
        // Completed (see FieldTechnician DetailsModel.OnPostSaveAsync) - null
        // until then. Distinct from DateOfCompletion (the deadline) above.
        public DateTime? DateCompleted { get; set; }

        // The manager who created this ticket - set automatically from the
        // logged-in manager's session at creation time (see JobTickets
        // CreateModel.OnPostAsync), never chosen from a form field. Null only
        // for tickets that predate this field.
        public int? AssignedByEmployeeID { get; set; }

        [ForeignKey(nameof(AssignedByEmployeeID))]
        public Employee? AssignedByEmployee { get; set; }

        // Free-text address entered directly by the manager.
        [Required(ErrorMessage = "Location address is required.")]
        [StringLength(300)]
        public string LocationAddress { get; set; } = string.Empty;

        // Free-text nearby landmark to help the field technician find the site.
        [StringLength(300)]
        public string? NearestLandmark { get; set; }

        // Always starts as "Pending" on creation; never exposed on the Create form.
        // Valid values: Pending, In Progress, Completed, Cancelled
        public string Status { get; set; } = JobTicketStatuses.Pending;

        // Free-text notes from the field technician leader about the current status.
        // Set alongside Status via the leader's consolidated Save action.
        [StringLength(500)]
        public string? Remarks { get; set; }

        // Once the field technician leader marks the job Completed, the ticket is
        // locked from further edits/status changes by anyone — field technician
        // or manager. Completed is terminal; there is no separate "close" step.
        // Cancelled is equally terminal - there's nothing left to schedule,
        // reassign, or update once a job has been called off.
        [NotMapped]
        public bool IsLockedFromEditing =>
            Status == JobTicketStatuses.Completed || Status == JobTicketStatuses.Cancelled;

        // Field technicians additionally cannot upload files or change
        // Status/Remarks while a Reschedule Request they submitted is still
        // awaiting the manager's decision — the manager must approve/act on it
        // (directly reschedule) before the leader can resume updating the ticket.
        // The manager themselves is NOT subject to this extra lock, since they're
        // the one who needs to act on the request.
        [NotMapped]
        public bool IsLockedFromFieldTechnicianEditing =>
            IsLockedFromEditing || Status == JobTicketStatuses.RescheduleRequest;

        // Overdue is just an expired Pending/In Progress job (see
        // JobTicketOverdueChecker) — for manager actions that are gated on
        // "still Pending" (e.g. re-designating the leader on the Edit page),
        // an Overdue ticket that was Pending should be treated the same way.
        // A Completed ticket whose actual completion date landed after the
        // deadline (DateOfCompletion) was finished late.
        [NotMapped]
        public bool IsCompletedLate =>
            Status == JobTicketStatuses.Completed
            && DateCompleted.HasValue
            && DateOfCompletion.HasValue
            && DateCompleted.Value.Date > DateOfCompletion.Value.Date;

        // What to show wherever Status is displayed: same as Status, except a
        // late completion reads as "Completed Late" instead of "Completed".
        [NotMapped]
        public string DisplayStatus => IsCompletedLate ? JobTicketStatuses.CompletedLate : Status;

        [NotMapped]
        public bool IsPendingOrOverdue =>
            Status == JobTicketStatuses.Pending || Status == JobTicketStatuses.Overdue;

        // The manager can re-designate the team leader from the Edit page any
        // time before the job is done - Pending/Overdue (not yet started) or
        // In Progress (started, but the leader can still change). Once
        // Completed, Edit is locked entirely (see IsLockedFromEditing) so this
        // never comes into play there.
        [NotMapped]
        public bool CanReassignLeader =>
            IsPendingOrOverdue || Status == JobTicketStatuses.InProgress;

        // The field technician leader can only open/act on a job ticket once its
        // scheduled date actually arrives — no working on (or marking complete)
        // a job that's still scheduled for a future date.
        [NotMapped]
        public bool IsAccessibleToFieldTechnician => DateTime.Now.Date >= ServiceDate.Date;

        // Label shown above the ServiceDate field/value, based on JobType.
        [NotMapped]
        public string ServiceDateLabel => JobType switch
        {
            JobTypes.Repair => "Date of Repair",
            JobTypes.Maintenance => "Date of Maintenance",
            _ => "Date of Installation"
        };

        // Navigation
        public ICollection<JobTicketAssignment> Assignments { get; set; }
            = new List<JobTicketAssignment>();

        public ICollection<JobTicketSubmission> Submissions { get; set; }
            = new List<JobTicketSubmission>();

        // History of date changes made by the manager. Each entry snapshots what
        // the ticket looked like (status, remarks) right before that reschedule,
        // and the submissions that were archived at that point.
        public ICollection<JobTicketRescheduleHistory> RescheduleHistory { get; set; }
            = new List<JobTicketRescheduleHistory>();

        // History of Submission — every status/remarks update the field
        // technician leader has saved for this ticket, in order, along with
        // whatever photos/files were attached at the time of each save.
        public ICollection<JobTicketSubmissionHistory> SubmissionHistory { get; set; }
            = new List<JobTicketSubmissionHistory>();
    }

    // A manager-maintained fiber plan option, offered in the "Fiber Plan"
    // dropdown on the Job Ticket Create page (Installation jobs only). Managers
    // can add or remove entries at runtime via the "Fiber Plans" modal — see
    // Pages/Manager/JobTickets/Create.cshtml(.cs). JobTicket.FiberPlan stores the
    // chosen plan's PlanName directly (not a foreign key), so removing a plan
    // here never affects tickets that already used it.
    [Table("tbl_fiberplan")]
    public class FiberPlan
    {
        [Key]
        public int FiberPlanID { get; set; }

        [Required(ErrorMessage = "Please enter a fiber plan name.")]
        [StringLength(FiberPlanRules.MaxLength, ErrorMessage = "Fiber plan name is too long (max 50 characters).")]
        [RegularExpression(FiberPlanRules.AllowedPattern, ErrorMessage = "Only letters, numbers, spaces, and . , - / ₱ # & ( ) are allowed.")]
        public string PlanName { get; set; } = string.Empty;

        public DateTime DateCreated { get; set; } = DateTime.Now;
    }
}
