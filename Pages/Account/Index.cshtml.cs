using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.Account
{
    // Self-service "My Account" for any logged-in Manager, Field Technician,
    // or Office Staff: view their own employee/account info and change their
    // own password. Admin isn't offered this page - see Program.cs RBAC
    // middleware - it manages its own account through Admin > User
    // Management like everyone else's.
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        public IndexModel(AppDbContext context) => _context = context;

        public UserAccount Account { get; set; } = default!;

        // Picked from the logged-in role so this one page still shows the
        // right sidebar for whoever's viewing it.
        public string LayoutName { get; set; } = "_Layout";

        [BindProperty]
        public string CurrentPassword { get; set; } = string.Empty;

        [BindProperty]
        public string NewPassword { get; set; } = string.Empty;

        [BindProperty]
        public string ConfirmNewPassword { get; set; } = string.Empty;

        public string? Message { get; set; }
        public string? Error { get; set; }

        // Personal "Employee Workload Summary" card - same signals/weighting as
        // Manager > Workload Monitoring's Employee Workload Summary tab, just
        // scoped to this one logged-in employee instead of every employee.
        public WorkloadSummary Workload { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var redirect = await LoadAsync();
            return redirect ?? Page();
        }

        public async Task<IActionResult> OnPostChangePasswordAsync()
        {
            var redirect = await LoadAsync();
            if (redirect != null) return redirect;

            if (!PasswordHasher.Verify(CurrentPassword ?? string.Empty, Account.PasswordHash, Account.PasswordSalt))
            {
                Error = "Current password is incorrect.";
                return Page();
            }

            if (string.IsNullOrWhiteSpace(NewPassword) || !Regex.IsMatch(NewPassword, UsernamePolicy.PasswordPattern))
            {
                Error = "New password must be 8-10 digits (numbers only).";
                return Page();
            }

            if (NewPassword != ConfirmNewPassword)
            {
                Error = "New passwords do not match.";
                return Page();
            }

            if (NewPassword == CurrentPassword)
            {
                Error = "New password must be different from your current password.";
                return Page();
            }

            var (hash, salt) = PasswordHasher.Hash(NewPassword);
            Account.PasswordHash = hash;
            Account.PasswordSalt = salt;
            await _context.SaveChangesAsync();

            Message = "Password changed successfully.";
            return Page();
        }

        // Loads the logged-in user's own account and picks the matching
        // sidebar layout for their role. Returns a redirect if the session is
        // missing/stale, otherwise null once Account/LayoutName are set.
        private async Task<IActionResult?> LoadAsync()
        {
            var employeeId = HttpContext.Session.GetInt32("AuthEmployeeId");
            var role = HttpContext.Session.GetString("AuthRoleType");

            if (employeeId == null || role == null)
            {
                return RedirectToPage("/Login");
            }

            LayoutName = role switch
            {
                "FieldTechnician" => "_FieldTechnicianLayout",
                "OfficeStaff" => "_OfficeStaffLayout",
                _ => "_Layout"
            };

            var account = await _context.UserAccounts
                .Include(a => a.Employee).ThenInclude(e => e!.Department)
                .FirstOrDefaultAsync(a => a.EmployeeID == employeeId.Value);

            if (account == null)
            {
                return RedirectToPage("/Login");
            }

            Account = account;
            Workload = account.Employee != null
                ? await BuildWorkloadSummaryAsync(account.Employee.EmployeeId, account.Employee.RoleType)
                : new WorkloadSummary();
            return null;
        }

        // Mirrors Manager/WorkLoadMonitoring/IndexModel's BuildWorkloadSummary /
        // BuildTechnicianWorkloadSummary weighting for a single employee: Office
        // Staff are measured by tasks + pending activities, Field Technicians by
        // job tickets. Managers/Admins have no personal workload in this schema.
        private async Task<WorkloadSummary> BuildWorkloadSummaryAsync(int employeeId, RoleType role)
        {
            var today = DateTime.Now.Date;

            if (role == RoleType.FieldTechnician)
            {
                var tickets = await _context.JobTickets
                    .Where(t => t.Assignments.Any(a => a.EmployeeID == employeeId))
                    .ToListAsync();

                var active = tickets.Count(t => t.Status is JobTicketStatuses.Pending or JobTicketStatuses.InProgress);
                var overdue = tickets.Count(t =>
                    t.Status is JobTicketStatuses.Pending or JobTicketStatuses.InProgress
                    && t.ServiceDate.Date < today);
                var completed = tickets.Count(t => t.Status == JobTicketStatuses.Completed);
                var points = (active * 2) + (overdue * 2);

                return new WorkloadSummary
                {
                    Kind = "Technician",
                    Active = active,
                    Overdue = overdue,
                    Completed = completed,
                    WorkloadPoints = points,
                    WorkloadLevel = points switch { <= 2 => "Light", <= 6 => "Moderate", _ => "Heavy" }
                };
            }

            if (role == RoleType.OfficeStaff)
            {
                var tasks = await _context.OfficeTasks
                    .Where(t => t.Assignments.Any(a => a.EmployeeID == employeeId))
                    .ToListAsync();

                var active = tasks.Count(t => t.Status is "Pending" or "In Progress" or "Overdue");
                var overdue = tasks.Count(t => t.Status == "Overdue");
                var completed = tasks.Count(t => t.Status == "Completed");

                var pendingActivities = await _context.TaskActivities
                    .CountAsync(a => a.AssignedEmployeeID == employeeId && a.Status != "Approved");

                var avgScore = tasks.Any() ? Math.Round(tasks.Average(t => t.Score), 1) : 0;
                var points = (active * 2) + pendingActivities + (overdue * 2);

                return new WorkloadSummary
                {
                    Kind = "Staff",
                    Active = active,
                    Overdue = overdue,
                    Completed = completed,
                    PendingActivities = pendingActivities,
                    AvgScore = avgScore,
                    WorkloadPoints = points,
                    WorkloadLevel = points switch { <= 2 => "Light", <= 6 => "Moderate", _ => "Heavy" }
                };
            }

            return new WorkloadSummary();
        }

        public class WorkloadSummary
        {
            // "Technician", "Staff", or "None" (Managers/Admins have no personal workload).
            public string Kind { get; set; } = "None";
            public int Active { get; set; }
            public int Overdue { get; set; }
            public int Completed { get; set; }
            public int PendingActivities { get; set; }
            public decimal AvgScore { get; set; }
            public int WorkloadPoints { get; set; }
            public string WorkloadLevel { get; set; } = "Light";
        }
    }
}
