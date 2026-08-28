using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TM_PE.Model
{
    [Table("tbl_taskassignment")]
    public class TaskAssignment
    {
        [Key]
        public int TaskAssignmentID { get; set; }

        public int OfficeTaskID { get; set; }

        [ForeignKey(nameof(OfficeTaskID))]
        public OfficeTask? OfficeTask { get; set; }

        public int EmployeeID { get; set; }

        [ForeignKey(nameof(EmployeeID))]
        public Employee? Employee { get; set; }

        public DateTime AssignedDate { get; set; } = DateTime.Now;
    }
}
