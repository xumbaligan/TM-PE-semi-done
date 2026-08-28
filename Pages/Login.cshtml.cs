using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages
{
    // The single front door for every role. Replaces the old "pick your name
    // from a dropdown" Select pages for Field Technician / Office Staff now
    // that real accounts exist (see Admin > User Management).
    public class LoginModel : PageModel
    {
        private readonly AppDbContext _context;
        public LoginModel(AppDbContext context) => _context = context;

        [BindProperty]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
            // Logging in again always starts clean.
            HttpContext.Session.Clear();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var username = Username?.Trim();
            var password = Password ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "Please enter both a username and password.";
                return Page();
            }

            var account = await _context.UserAccounts
                .Include(a => a.Employee)
                .FirstOrDefaultAsync(a => a.Username.ToLower() == username.ToLower());

            if (account == null || account.Employee == null ||
                !account.IsActive || !account.Employee.IsActive ||
                !PasswordHasher.Verify(password, account.PasswordHash, account.PasswordSalt))
            {
                ErrorMessage = "Invalid username or password.";
                return Page();
            }

            var employee = account.Employee;

            HttpContext.Session.Clear();
            HttpContext.Session.SetInt32("AuthEmployeeId", employee.EmployeeId);
            HttpContext.Session.SetString("AuthEmployeeName", employee.FullName);
            HttpContext.Session.SetString("AuthRoleType", employee.RoleType.ToString());
            HttpContext.Session.SetString("AuthUsername", account.Username);

            if (employee.RoleType == RoleType.FieldTechnician)
                HttpContext.Session.SetInt32("CurrentFieldTechnicianId", employee.EmployeeId);
            else if (employee.RoleType == RoleType.OfficeStaff)
                HttpContext.Session.SetInt32("CurrentEmployeeId", employee.EmployeeId);

            account.LastLogin = DateTime.Now;
            await _context.SaveChangesAsync();

            return employee.RoleType switch
            {
                RoleType.Admin => RedirectToPage("/Admin/Employees/Index"),
                RoleType.Manager => RedirectToPage("/Index"),
                RoleType.FieldTechnician => RedirectToPage("/FieldTechnician/Index"),
                RoleType.OfficeStaff => RedirectToPage("/OfficeStaff/Index"),
                _ => RedirectToPage("/Login")
            };
        }
    }
}