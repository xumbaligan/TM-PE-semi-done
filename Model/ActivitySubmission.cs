using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TM_PE.Model
{
    [Table("tbl_activitysubmission")]
    public class ActivitySubmission
    {
        [Key]
        public int SubmissionID { get; set; }

        public int ActivityID { get; set; }

        [ForeignKey(nameof(ActivityID))]
        public TaskActivity? Activity { get; set; }

        public int EmployeeID { get; set; }

        [ForeignKey(nameof(EmployeeID))]
        public Employee? Employee { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public DateTime DateSubmitted { get; set; } = DateTime.Now;

        // "Pending Review" until a manager approves/rejects this specific
        // attempt (see Manager/OfficeTask DetailsModel.OnPostSaveAsync), then
        // "Approved" or "Rejected" - permanently, since each re-upload creates
        // a new ActivitySubmission row rather than overwriting this one. That
        // history (this attempt's own file, feedback, and who/when reviewed
        // it) is what lets the manager see everything a re-upload used to
        // silently erase.
        public string Status { get; set; } = "Pending Review";

        // The manager's feedback for THIS attempt specifically - distinct from
        // TaskActivity.FeedBack, which only ever holds the current/most recent
        // feedback for quick display.
        [StringLength(500)]
        public string? Feedback { get; set; }

        public int? ReviewedByEmployeeID { get; set; }

        [ForeignKey(nameof(ReviewedByEmployeeID))]
        public Employee? ReviewedByEmployee { get; set; }

        public DateTime? DateReviewed { get; set; }
    }
}
