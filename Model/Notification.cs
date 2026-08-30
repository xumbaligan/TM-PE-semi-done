using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TM_PE.Model
{
    [Table("tbl_notifications")]
    public class Notification
    {
        [Key]
        public int NotificationID { get; set; }

        public int EmployeeID { get; set; }
        [ForeignKey(nameof(EmployeeID))]
        public Employee? Employee { get; set; }

        [Required, StringLength(300)]
        public string Message { get; set; } = string.Empty;

        // Page-relative link the notification opens when clicked (e.g.
        // "/FieldTechnician/Details/12"); null when it has nowhere specific to go.
        [StringLength(300)]
        public string? Url { get; set; }

        // A Bootstrap Icons class shown next to the message in the dropdown -
        // purely cosmetic, defaults to a plain bell.
        [StringLength(40)]
        public string Icon { get; set; } = "bi-bell";

        public bool IsRead { get; set; } = false;

        public DateTime DateCreated { get; set; } = DateTime.Now;
    }
}
