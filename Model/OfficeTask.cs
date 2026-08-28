using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TM_PE.Model
{
    [Table("tbl_officetask")]
    public class OfficeTask
    {
        [Key]
        public int OfficeTaskID { get; set; }

        [Required]
        [StringLength(20)]
        public string TaskNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string TaskName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime DateCreated { get; set; } = DateTime.Now;

        public DateTime DueDate { get; set; } = DateTime.Now;

        // The date every activity got Approved, making the task Completed
        // (see RecalculateTaskAsync) - null until then. Distinct from DueDate
        // above, which is just the deadline.
        public DateTime? DateCompleted { get; set; }

        // The manager who created this task - set automatically from the
        // logged-in manager's session at creation time (see OfficeTask
        // CreateModel.OnPostAsync), never chosen from a form field. Null only
        // for tasks that predate this field.
        public int? AssignedByEmployeeID { get; set; }

        [ForeignKey(nameof(AssignedByEmployeeID))]
        public Employee? AssignedByEmployee { get; set; }

        public string Status { get; set; } = "Pending";

        public decimal Progress { get; set; } = 0;

        public decimal Score { get; set; } = 0;

        // A Completed task whose actual completion date landed after DueDate
        // was finished late.
        [NotMapped]
        public bool IsCompletedLate =>
            Status == "Completed" && DateCompleted.HasValue && DateCompleted.Value.Date > DueDate.Date;

        // What to show wherever Status is displayed: same as Status, except a
        // late completion reads as "Completed Late" instead of "Completed".
        [NotMapped]
        public string DisplayStatus => IsCompletedLate ? "Completed Late" : Status;

        // Navigation
        public ICollection<TaskActivity> Activities { get; set; }
            = new List<TaskActivity>();

        public ICollection<TaskAssignment> Assignments { get; set; }
            = new List<TaskAssignment>();
    }
}
