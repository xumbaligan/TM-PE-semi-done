using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TM_PE.Model
{
    public enum AppraisalStatus
    {
        Draft,
        Finalized
    }

    // Appraisal is the management decision made *after* a Performance
    // Evaluation and its Feedback exist: Performance Evaluation + Feedback ->
    // Appraisal. It's a separate historical record — it doesn't modify the
    // evaluation, and OverallRating below is a point-in-time copy of the
    // evaluation's rating rather than a live reference, so this appraisal
    // reads the same way years later even if the evaluation is edited.
    [Table("tbl_appraisal")]
    public class Appraisal
    {
        [Key]
        public int AppraisalID { get; set; }

        [Required]
        public int EmployeeID { get; set; }

        [ForeignKey(nameof(EmployeeID))]
        public Employee? Employee { get; set; }

        [Required(ErrorMessage = "Please select the Performance Evaluation this appraisal is based on.")]
        public int EvaluationID { get; set; }

        [ForeignKey(nameof(EvaluationID))]
        public PerformanceEvaluation? Evaluation { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime AppraisalDate { get; set; } = DateTime.Now;

        // Snapshot of Evaluation.OverallRating at the time this appraisal was made.
        [StringLength(50)]
        public string OverallRating { get; set; } = string.Empty;

        [Column(TypeName = "nvarchar(50)")]
        public AppraisalRecommendation Recommendation { get; set; } = AppraisalRecommendation.NoAction;

        public bool SalaryAdjustmentRecommendation { get; set; }
        public bool PromotionRecommendation { get; set; }
        public bool TrainingRecommendation { get; set; }

        [StringLength(1000)]
        public string? ManagerRemarks { get; set; }

        [Column(TypeName = "nvarchar(20)")]
        public AppraisalStatus AppraisalStatus { get; set; } = AppraisalStatus.Draft;

        public DateTime DateCreated { get; set; } = DateTime.Now;

        // Builds a human-readable combined label like "Training + Recognition" by
        // pairing the primary Recommendation with any supplementary flags that
        // aren't already implied by it. Used by Index/Details and the
        // Performance Report so this logic lives in one place, not duplicated
        // across Razor pages.
        public string CombinedRecommendationLabel()
        {
            var parts = new List<string>();

            if (TrainingRecommendation && Recommendation != AppraisalRecommendation.TrainingRequired)
                parts.Add("Training");
            if (PromotionRecommendation && Recommendation != AppraisalRecommendation.PromotionRecommended)
                parts.Add("Promotion");
            if (SalaryAdjustmentRecommendation && Recommendation != AppraisalRecommendation.SalaryAdjustmentRecommended)
                parts.Add("Salary Adjustment");

            parts.Add(RecommendationLabel(Recommendation));

            return string.Join(" + ", parts);
        }

        public static string RecommendationLabel(AppraisalRecommendation r) => r switch
        {
            AppraisalRecommendation.NoAction => "No Action",
            AppraisalRecommendation.Recognition => "Recognition",
            AppraisalRecommendation.TrainingRequired => "Training Required",
            AppraisalRecommendation.PerformanceImprovementPlan => "Performance Improvement Plan",
            AppraisalRecommendation.PromotionRecommended => "Promotion Recommended",
            AppraisalRecommendation.SalaryAdjustmentRecommended => "Salary Adjustment Recommended",
            _ => r.ToString()
        };
    }
}