using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TM_PE.Model
{
    [Table("tbl_employees")]
    public class Employee
    {
        [Key]
        public int EmployeeId { get; set; }
        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, ErrorMessage = "Max 100 characters.")]
        [RegularExpression(@"^[a-zA-Z\s]+$",
            ErrorMessage = "Full Name must contain letters only. No numbers or symbols allowed.")]
        public string FullName { get; set; } = string.Empty;
        [BindProperty]
        [Required, EmailAddress, StringLength(150)]
        public string Email { get; set; } = string.Empty;
        //[StringLength(50)]
        //public string? Position { get; set; }
        [Column(TypeName = "nvarchar(50)")]
        public RoleType RoleType { get; set; }
        public int DepartmentId { get; set; }
        [ForeignKey(nameof(DepartmentId))]
        public Department? Department { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime HiredAt { get; set; } = DateTime.UtcNow;
        public ICollection<TaskAssignment> TaskAssignments { get; set; }
        = new List<TaskAssignment>();
    }
}
