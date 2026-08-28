using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TM_PE.Model
{
    // Represents a picture/file the leader of the assigned field technicians uploaded
    // as proof of work for a job ticket. Populated by the (future) Field Technician
    // portal; the Manager interface only views these.
    [Table("tbl_jobticketsubmission")]
    public class JobTicketSubmission
    {
        [Key]
        public int JobTicketSubmissionID { get; set; }

        public int JobTicketID { get; set; }

        [ForeignKey(nameof(JobTicketID))]
        public JobTicket? JobTicket { get; set; }

        // The leader who uploaded the file.
        public int EmployeeID { get; set; }

        [ForeignKey(nameof(EmployeeID))]
        public Employee? Employee { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public DateTime DateSubmitted { get; set; } = DateTime.Now;

        // Null = belongs to the ticket's current cycle (shows in the active
        // "Submitted Photos / Files" list). Set when a manager reschedules the
        // ticket's service date, archiving this submission under that history
        // entry so it shows up as "previous" evidence instead.
        public int? RescheduleHistoryID { get; set; }

        [ForeignKey(nameof(RescheduleHistoryID))]
        public JobTicketRescheduleHistory? RescheduleHistory { get; set; }

        // Null = belongs to the ticket's current, not-yet-saved cycle (shows in
        // the active "Submitted Photos / Files" list). Set when the field
        // technician leader saves a status/remarks update, archiving this
        // submission under that "History of Submission" entry instead.
        public int? SubmissionHistoryID { get; set; }

        [ForeignKey(nameof(SubmissionHistoryID))]
        public JobTicketSubmissionHistory? SubmissionHistory { get; set; }
    }
}
