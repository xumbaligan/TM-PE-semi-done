using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TM_PE.Model
{
    public enum FeedbackType
    {
        Positive,
        Recognition,
        Improvement,
        Concern,
        General
    }

    // Qualitative notes that a numeric Performance Evaluation score can't fully
    // capture. Feedback is intentionally its own historical trail: creating a
    // new Performance Evaluation never touches or overwrites past Feedback
    // records, and Feedback can be logged with or without being tied to a
    // specific evaluation.
    [Table("tbl_feedback")]
    public class Feedback
    {
        [Key]
        public int FeedbackID { get; set; }

        [Required]
        public int EmployeeID { get; set; }

        [ForeignKey(nameof(EmployeeID))]
        public Employee? Employee { get; set; }

        [Required(ErrorMessage = "Please enter who is submitting this feedback.")]
        [StringLength(100)]
        public string SubmittedBy { get; set; } = "Manager";

        // Optional — feedback can stand on its own (e.g. logged the moment it
        // happens) or be attached to the evaluation period it relates to.
        public int? EvaluationID { get; set; }

        [ForeignKey(nameof(EvaluationID))]
        public PerformanceEvaluation? Evaluation { get; set; }

        [Column(TypeName = "nvarchar(20)")]
        public FeedbackType FeedbackType { get; set; } = FeedbackType.General;

        [Required(ErrorMessage = "Please enter a comment.")]
        [StringLength(1000)]
        public string Comment { get; set; } = string.Empty;

        public DateTime DateCreated { get; set; } = DateTime.Now;
    }
}