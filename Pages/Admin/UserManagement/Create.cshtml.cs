using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.Admin.UserManagement
{
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;
        public CreateModel(AppDbContext context) => _context = context;

        [BindProperty]
        public int EmployeeID { get; set; }

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        public string ConfirmPassword { get; set; } = string.Empty;

        // Employees who don't already have a user account — one account per
        // employee.
        public List<Employee> EmployeeList { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadEmployeeListAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var employee = await _context.Employees.FindAsync(EmployeeID);
            if (employee == null)
            {
                ModelState.AddModelError(nameof(EmployeeID), "Please select an employee.");
            }
            else if (await _context.UserAccounts.AnyAsync(a => a.EmployeeID == employee.EmployeeId))
            {
                ModelState.AddModelError(nameof(EmployeeID), "This employee already has a user account.");
            }

            if (string.IsNullOrWhiteSpace(Password) ||
                !System.Text.RegularExpressions.Regex.IsMatch(Password, UsernamePolicy.PasswordPattern))
            {
                ModelState.AddModelError(nameof(Password), "Password must be 8-10 digits (numbers only).");
            }
            else if (Password != ConfirmPassword)
            {
                ModelState.AddModelError(nameof(ConfirmPassword), "Passwords do not match.");
            }

            if (!ModelState.IsValid || employee == null)
            {
                await LoadEmployeeListAsync();
                return Page();
            }

            // Username is fixed and derived server-side — never trust a
            // client-supplied value for it.
            var username = UsernamePolicy.BuildUsername(employee);

            if (await _context.UserAccounts.AnyAsync(a => a.Username == username))
            {
                ModelState.AddModelError(nameof(EmployeeID), "A user account with this username already exists.");
                await LoadEmployeeListAsync();
                return Page();
            }

            var (hash, salt) = PasswordHasher.Hash(Password);

            var account = new UserAccount
            {
                EmployeeID = employee.EmployeeId,
                Username = username,
                PasswordHash = hash,
                PasswordSalt = salt,
                DateCreated = DateTime.Now,
                IsActive = true
            };

            _context.UserAccounts.Add(account);
            await _context.SaveChangesAsync();

            return RedirectToPage("Index");
        }

        private async Task LoadEmployeeListAsync()
        {
            var employeeIdsWithAccounts = await _context.UserAccounts
                .Select(a => a.EmployeeID)
                .ToListAsync();

            EmployeeList = await _context.Employees
                .Where(e => e.IsActive && !employeeIdsWithAccounts.Contains(e.EmployeeId))
                .OrderBy(e => e.FullName)
                .ToListAsync();
        }
    }
}