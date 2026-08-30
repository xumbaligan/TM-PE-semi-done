using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages
{
    // Step 3 of Forgot Password: only reachable once VerifyCodeModel has
    // confirmed the emailed code, which sets PwReset_VerifiedEmployeeId in
    // session - there's no other way in. Sets a brand new password for that
    // account (same 8-10 digit numeric rule Admin > User Management uses),
    // then sends the visitor back to Login to sign in with it; this page
    // never signs anyone in itself.
    public class ResetPasswordModel : PageModel
    {
        private readonly AppDbContext _context;
        public ResetPasswordModel(AppDbContext context) => _context = context;

        [BindProperty]
        public string NewPassword { get; set; } = string.Empty;

        [BindProperty]
        public string ConfirmNewPassword { get; set; } = string.Empty;

        // Explicit, page-scoped key - see ForgotPasswordModel.ErrorMessage
        // for why: ForgotPassword/VerifyCode each declare their own
        // same-named "ErrorMessage" property, so without distinct keys all
        // three would share one TempData slot.
        [TempData(Key = "RP_ErrorMessage")]
        public string? ErrorMessage { get; set; }

        public string Username { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            var employeeId = HttpContext.Session.GetInt32("PwReset_VerifiedEmployeeId");
            if (employeeId == null)
            {
                return RedirectToPage("/ForgotPassword");
            }

            Username = await _context.UserAccounts
                .Where(a => a.EmployeeID == employeeId.Value)
                .Select(a => a.Username)
                .FirstOrDefaultAsync() ?? "";

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var employeeId = HttpContext.Session.GetInt32("PwReset_VerifiedEmployeeId");
            if (employeeId == null)
            {
                return RedirectToPage("/ForgotPassword");
            }

            var account = await _context.UserAccounts
                .Include(a => a.Employee)
                .FirstOrDefaultAsync(a => a.EmployeeID == employeeId.Value);

            if (account == null || account.Employee == null || !account.IsActive || !account.Employee.IsActive)
            {
                HttpContext.Session.Remove("PwReset_VerifiedEmployeeId");
                // Targets ForgotPassword's own TempData key directly, not
                // this page's ErrorMessage property - the message is meant
                // to appear after the redirect below, on that page.
                TempData["FP_ErrorMessage"] = "This account is no longer available.";
                return RedirectToPage("/ForgotPassword");
            }

            Username = account.Username;

            if (!Regex.IsMatch(NewPassword ?? "", UsernamePolicy.PasswordPattern))
            {
                ErrorMessage = "Password must be 8-10 digits, numbers only.";
                return Page();
            }

            if (NewPassword != ConfirmNewPassword)
            {
                ErrorMessage = "Passwords do not match.";
                return Page();
            }

            var (hash, salt) = PasswordHasher.Hash(NewPassword);
            account.PasswordHash = hash;
            account.PasswordSalt = salt;
            await _context.SaveChangesAsync();

            HttpContext.Session.Remove("PwReset_VerifiedEmployeeId");

            TempData["SuccessMessage"] = "Your password was changed. Please log in with your new password.";
            return RedirectToPage("/Login");
        }
    }
}
