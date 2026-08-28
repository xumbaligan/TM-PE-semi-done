using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.FieldTechnician
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public Employee CurrentEmployee { get; set; } = default!;

        public List<JobTicket> AssignedJobOrders { get; set; } = new();

        // Job orders where the current employee is the assigned leader.
        public HashSet<int> LeaderOn { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var employeeId = HttpContext.Session.GetInt32("CurrentFieldTechnicianId");
            if (employeeId == null)
            {
                return RedirectToPage("/Login");
            }

            var employee = await _context.Employees.FindAsync(employeeId.Value);
            if (employee == null || employee.RoleType != RoleType.FieldTechnician)
            {
                HttpContext.Session.Remove("CurrentFieldTechnicianId");
                return RedirectToPage("/Login");
            }

            CurrentEmployee = employee;

            AssignedJobOrders = await _context.JobTickets
                .Include(t => t.Assignments)
                .Include(t => t.AssignedByEmployee)
                .Where(t => t.Assignments.Any(a => a.EmployeeID == employeeId.Value))
                .OrderByDescending(t => t.DateCreated)
                .ToListAsync();

            // Overdue is purely time-based, so re-check it every time the list is
            // loaded rather than relying on whatever was last saved.
            await JobTicketOverdueChecker.RefreshAsync(_context, AssignedJobOrders);

            LeaderOn = AssignedJobOrders
                .Where(t => t.Assignments.Any(a => a.EmployeeID == employeeId.Value && a.IsLeader))
                .Select(t => t.JobTicketID)
                .ToHashSet();

            return Page();
        }
    }
}