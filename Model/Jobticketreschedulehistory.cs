using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TM_PE.Model
{
    // Logged every time a manager edits a job ticket's service date. Lets the
    // assigned field technician see why/when a job was moved, plus a snapshot
    // of the remarks and photos/files that were on the ticket before the move.
    [Table("tbl_jobticketreschedulehistory")]
    public class JobTicketRescheduleHistory
    {
        [Key]
        public int JobTicketRescheduleHistoryID { get; set; }

        public int JobTicketID { get; set; }

        [ForeignKey(nameof(JobTicketID))]
        public JobTicket? JobTicket { get; set; }

        public DateTime OldServiceDate { get; set; }

        public DateTime NewServiceDate { get; set; }

        // Reason for the reschedule. Managers are not required to provide one,
        // but the database column is non-nullable, so we preserve an empty value.
        [Required]
        [StringLength(500)]
        public string Reason { get; set; } = string.Empty;

        // Snapshot of the ticket's status/remarks immediately before this reschedule.
        [StringLength(20)]
        public string? PreviousStatus { get; set; }

        [StringLength(500)]
        public string? PreviousRemarks { get; set; }

        public DateTime DateChanged { get; set; } = DateTime.Now;

        // Photos/files that were on the ticket before this reschedule; archived here
        // so the technician's "current" submissions list starts fresh for the new date.
        public ICollection<JobTicketSubmission> ArchivedSubmissions { get; set; }
            = new List<JobTicketSubmission>();
    }
}
