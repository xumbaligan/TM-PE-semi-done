using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TM_PE.Model
{
    public enum RoleType
    {
        Admin,
        Manager,
        OfficeStaff,
        FieldTechnician
    }

    // Historically distinguished how a criterion's star rating got filled in
    // (WorkQuality by hand vs. JobCompletion/Timeliness auto-filled from a
    // technician's job ticket record). Every criterion is now rated by hand
    // regardless of this value - the Admin/Manager > Criteria forms no longer
    // expose a way to set it, and it always saves as WorkQuality for new or
    // edited criteria. Kept only so existing rows in tbl_criteria still load.
    public enum CriteriaMetricType
    {
        WorkQuality,
        JobCompletion,
        Timeliness
    }

    [Table("tbl_criteria")]
    public class Criteria
    {
        [Key]
        public int CriteriaId { get; set; }

        [Required, StringLength(150)]
        public string CriteriaName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Column(TypeName = "nvarchar(50)")]
        public RoleType RoleType { get; set; }

        // How much this criterion counts toward the Performance Evaluation's
        // Overall Score, in percentage points (e.g. 25 for "Work Quality — 25%").
        // Also doubles as the max points an evaluator can award for this
        // criterion, so weights for a given RoleType should add up to 100.
        [Range(0, 100, ErrorMessage = "Weight must be between 0 and 100.")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal Weight { get; set; } = 0;

        // Only meaningful (and only editable in the UI) for RoleType ==
        // FieldTechnician - an Office Staff criterion is always WorkQuality.
        [Column(TypeName = "nvarchar(50)")]
        public CriteriaMetricType MetricType { get; set; } = CriteriaMetricType.WorkQuality;

        public bool IsActive { get; set; } = true;
    }
}