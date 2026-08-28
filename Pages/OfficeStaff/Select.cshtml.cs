using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.OfficeStaff
{
    // Simple "who are you" picker. The app has no login system, so an Office
    // Staff employee just picks their name from a list; that choice is kept
    // in session for the rest of the Employee Tasks area.
    public class SelectModel : PageModel
    {
        private readonly AppDbContext _context;

        public SelectModel(AppDbContext context)
        {
            _context = context;
        }

        public List<Employee> OfficeStaff { get; set; } = new();

        public async Task OnGetAsync()
        {
            HttpContext.Session.Remove("CurrentEmployeeId");
            await LoadOfficeStaffAsync();
        }

        public async Task<IActionResult> OnPostAsync(int employeeId)
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId
                    && e.IsActive
                    && e.RoleType == RoleType.OfficeStaff);

            if (employee == null)
            {
                ModelState.AddModelError(string.Empty, "Please select your name from the list.");
                await LoadOfficeStaffAsync();
                return Page();
            }

            HttpContext.Session.SetInt32("CurrentEmployeeId", employee.EmployeeId);
            return RedirectToPage("./Index");
        }

        private async Task LoadOfficeStaffAsync()
        {
            OfficeStaff = await _context.Employees
                .Where(e => e.IsActive && e.RoleType == RoleType.OfficeStaff)
                .OrderBy(e => e.FullName)
                .ToListAsync();
        }
    }
}
