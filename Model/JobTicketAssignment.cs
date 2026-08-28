using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TM_PE.Model
{
    [Table("tbl_jobticketassignment")]
    public class JobTicketAssignment
    {
        [Key]
        public int JobTicketAssignmentID { get; set; }

        public int JobTicketID { get; set; }

        [ForeignKey(nameof(JobTicketID))]
        public JobTicket? JobTicket { get; set; }

        public int EmployeeID { get; set; }

        [ForeignKey(nameof(EmployeeID))]
        public Employee? Employee { get; set; }

        // Exactly one assignee per job ticket should have IsLeader = true.
        public bool IsLeader { get; set; } = false;

        public DateTime AssignedDate { get; set; } = DateTime.Now;
    }
}
