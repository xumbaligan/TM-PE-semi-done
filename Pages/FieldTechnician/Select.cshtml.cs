using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.FieldTechnician
{
    // Simple "who are you" picker. The app has no login system, so a Field
    // Technician just picks their name from a list; that choice is kept
    // in session for the rest of the Field Technician area.
    public class SelectModel : PageModel
    {
        private readonly AppDbContext _context;

        public SelectModel(AppDbContext context)
        {
            _context = context;
        }

        public List<Employee> FieldTechnicians { get; set; } = new();

        public async Task OnGetAsync()
        {
            HttpContext.Session.Remove("CurrentFieldTechnicianId");
            await LoadFieldTechniciansAsync();
        }

        public async Task<IActionResult> OnPostAsync(int employeeId)
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId
                    && e.IsActive
                    && e.RoleType == RoleType.FieldTechnician);

            if (employee == null)
            {
                ModelState.AddModelError(string.Empty, "Please select your name from the list.");
                await LoadFieldTechniciansAsync();
                return Page();
            }

            HttpContext.Session.SetInt32("CurrentFieldTechnicianId", employee.EmployeeId);
            return RedirectToPage("./Index");
        }

        private async Task LoadFieldTechniciansAsync()
        {
            FieldTechnicians = await _context.Employees
                .Where(e => e.IsActive && e.RoleType == RoleType.FieldTechnician)
                .OrderBy(e => e.FullName)
                .ToListAsync();
        }
    }
}
