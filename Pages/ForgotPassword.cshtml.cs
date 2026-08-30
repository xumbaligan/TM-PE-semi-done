using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;

namespace TM_PE.Pages
{
    // Step 1 of Forgot Password: the visitor enters the email on file for
    // their employee record. A match generates a 4-digit code, stashes it in
    // session (nothing persisted to the database - it's short-lived and
    // single-use), emails it via EmailService, and hands off to VerifyCode.
    public class ForgotPasswordModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly EmailService _emailService;

        public ForgotPasswordModel(AppDbContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [TempData]
        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
            HttpContext.Session.Remove("PwReset_EmployeeId");
            HttpContext.Session.Remove("PwReset_Email");
            HttpContext.Session.Remove("PwReset_Code");
            HttpContext.Session.Remove("PwReset_Expiry");
            HttpContext.Session.Remove("PwReset_Attempts");
            HttpContext.Session.Remove("PwReset_DevCode");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var email = (Email ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(email))
            {
                ErrorMessage = "Please enter your email address.";
                return Page();
            }

            // Only an active employee with an active login account can be
            // matched - a deactivated account has no way to use the code
            // anyway, and shouldn't be told whether its email exists.
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.IsActive && e.Email.ToLower() == email.ToLower());

            var hasActiveAccount = employee != null && await _context.UserAccounts
                .AnyAsync(a => a.EmployeeID == employee.EmployeeId && a.IsActive);

            if (employee == null || !hasActiveAccount)
            {
                ErrorMessage = "No active account was found with that email address.";
                return Page();
            }

            var code = Random.Shared.Next(0, 10000).ToString("D4");

            HttpContext.Session.SetInt32("PwReset_EmployeeId", employee.EmployeeId);
            HttpContext.Session.SetString("PwReset_Email", employee.Email);
            HttpContext.Session.SetString("PwReset_Code", code);
            HttpContext.Session.SetString("PwReset_Expiry", DateTime.UtcNow.AddMinutes(10).ToString("o"));
            HttpContext.Session.SetInt32("PwReset_Attempts", 0);

            var sent = await _emailService.TrySendCodeAsync(employee.Email, code);

            // Email delivery isn't configured/working yet - carry the code in
            // session (not TempData, which only survives a single redirect)
            // so VerifyCode can keep showing it across an incorrect-attempt
            // re-render instead of it vanishing after the first try.
            if (sent)
            {
                HttpContext.Session.Remove("PwReset_DevCode");
            }
            else
            {
                HttpContext.Session.SetString("PwReset_DevCode", code);
            }

            return RedirectToPage("/VerifyCode");
        }
    }
}
