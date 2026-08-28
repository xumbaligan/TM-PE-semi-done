using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.OfficeStaff
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public Employee CurrentEmployee { get; set; } = default!;

        public List<TM_PE.Model.OfficeTask> AssignedTasks { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var employeeId = HttpContext.Session.GetInt32("CurrentEmployeeId");
            if (employeeId == null)
            {
                return RedirectToPage("/Login");
            }

            var employee = await _context.Employees.FindAsync(employeeId.Value);
            if (employee == null)
            {
                HttpContext.Session.Remove("CurrentEmployeeId");
                return RedirectToPage("/Login");
            }

            CurrentEmployee = employee;

            AssignedTasks = await _context.OfficeTasks
                .Include(t => t.Activities)
                .Include(t => t.AssignedByEmployee)
                .Where(t => t.Assignments.Any(a => a.EmployeeID == employeeId.Value))
                .OrderByDescending(t => t.DateCreated)
                .ToListAsync();

            return Page();
        }
    }
}