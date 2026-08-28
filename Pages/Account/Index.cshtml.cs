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
            return null;
        }
    }
}
