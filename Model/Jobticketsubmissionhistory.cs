using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TM_PE.Model
{
    // Logged every time the field technician leader presses Save on a job
    // ticket's Update Status form — for ANY status (Pending, In Progress,
    // Completed, Cancelled, Rescheduled), not just reschedules. Each row is a
    // snapshot of what the leader submitted: the status/remarks that were set
    // and when, plus the photos/files that were attached to the ticket at
    // that point. Lets anyone viewing the ticket see the full history of
    // submissions the leader has made over time.
    [Table("tbl_jobticketsubmissionhistory")]
    public class JobTicketSubmissionHistory
    {
        [Key]
        public int JobTicketSubmissionHistoryID { get; set; }

        public int JobTicketID { get; set; }

        [ForeignKey(nameof(JobTicketID))]
        public JobTicket? JobTicket { get; set; }

        // The status the leader set the ticket to on this save.
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = string.Empty;

        // The remarks the leader entered alongside this status change.
        [StringLength(500)]
        public string? Remarks { get; set; }

        public DateTime DateChanged { get; set; } = DateTime.Now;

        // Who made this change - the field technician leader for every status
        // (see FieldTechnician DetailsModel.OnPostSaveAsync), or the manager
        // for the "Rescheduled" entry logged when they change the service
        // date (see Manager JobTickets EditModel.OnPostAsync). Null only for
        // entries that predate this field.
        public int? ActorEmployeeID { get; set; }

        [ForeignKey(nameof(ActorEmployeeID))]
        public Employee? ActorEmployee { get; set; }

        // Photos/files that were on the ticket at the time of this save;
        // archived here so the "current" submissions list starts fresh for the
        // leader's next update, while still being viewable from this history
        // entry's "View" modal.
        public ICollection<JobTicketSubmission> ArchivedSubmissions { get; set; }
            = new List<JobTicketSubmission>();
    }
}