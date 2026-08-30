using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages
{
    // Step 2 of Forgot Password: verifies the 4-digit code stashed in session
    // by ForgotPasswordModel. A correct, unexpired code logs the visitor in
    // exactly like Login.cshtml.cs does - same session keys, same
    // per-role redirect - so verifying the code is itself how they regain
    // account access; their existing password is untouched.
    public class VerifyCodeModel : PageModel
    {
        private const int MaxAttempts = 5;

        private readonly AppDbContext _context;
        private readonly EmailService _emailService;

        public VerifyCodeModel(AppDbContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        [BindProperty]
        public string Code { get; set; } = string.Empty;

        [TempData]
        public string? ErrorMessage { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        public string MaskedEmail { get; set; } = string.Empty;

        // Only set when EmailService couldn't actually send the code (no SMTP
        // configured yet) - shown as a fallback so the flow is still testable
        // end-to-end before real email credentials exist.
        public string? DevCode { get; set; }

        public IActionResult OnGet()
        {
            var employeeId = HttpContext.Session.GetInt32("PwReset_EmployeeId");
            if (employeeId == null)
            {
                return RedirectToPage("/ForgotPassword");
            }

            MaskedEmail = MaskEmail(HttpContext.Session.GetString("PwReset_Email") ?? "");

            if (TempData["PwReset_DevCode"] is string devCode)
            {
                DevCode = devCode;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var employeeId = HttpContext.Session.GetInt32("PwReset_EmployeeId");
            var storedCode = HttpContext.Session.GetString("PwReset_Code");
            var expiryRaw = HttpContext.Session.GetString("PwReset_Expiry");

            if (employeeId == null || storedCode == null || expiryRaw == null)
            {
                return RedirectToPage("/ForgotPassword");
            }

            MaskedEmail = MaskEmail(HttpContext.Session.GetString("PwReset_Email") ?? "");

            if (!DateTime.TryParse(expiryRaw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiry)
                || DateTime.UtcNow > expiry)
            {
                ClearPendingReset();
                ErrorMessage = "That code has expired. Please request a new one.";
                return RedirectToPage("/ForgotPassword");
            }

            if ((Code ?? string.Empty).Trim() != storedCode)
            {
                var attempts = (HttpContext.Session.GetInt32("PwReset_Attempts") ?? 0) + 1;

                if (attempts >= MaxAttempts)
                {
                    ClearPendingReset();
                    ErrorMessage = "Too many incorrect attempts. Please request a new code.";
                    return RedirectToPage("/ForgotPassword");
                }

                HttpContext.Session.SetInt32("PwReset_Attempts", attempts);
                ErrorMessage = $"Incorrect code. {MaxAttempts - attempts} attempt(s) left.";
                return Page();
            }

            var employee = await _context.Employees.FindAsync(employeeId.Value);
            var account = await _context.UserAccounts
                .FirstOrDefaultAsync(a => a.EmployeeID == employeeId.Value);

            if (employee == null || account == null || !employee.IsActive || !account.IsActive)
            {
                ClearPendingReset();
                ErrorMessage = "This account is no longer available.";
                return RedirectToPage("/ForgotPassword");
            }

            // Code confirmed - drop the one-time code state, but keep a
            // marker recording which account just verified, so ResetPassword
            // can let them set a new password without a full login. Nobody
            // is signed in yet; that only happens once they actually log in
            // with the new password on /Login.
            HttpContext.Session.Remove("PwReset_EmployeeId");
            HttpContext.Session.Remove("PwReset_Email");
            HttpContext.Session.Remove("PwReset_Code");
            HttpContext.Session.Remove("PwReset_Expiry");
            HttpContext.Session.Remove("PwReset_Attempts");
            HttpContext.Session.SetInt32("PwReset_VerifiedEmployeeId", employee.EmployeeId);

            return RedirectToPage("/ResetPassword");
        }

        public async Task<IActionResult> OnPostResendAsync()
        {
            var employeeId = HttpContext.Session.GetInt32("PwReset_EmployeeId");
            var email = HttpContext.Session.GetString("PwReset_Email");

            if (employeeId == null || string.IsNullOrEmpty(email))
            {
                return RedirectToPage("/ForgotPassword");
            }

            var code = Random.Shared.Next(0, 10000).ToString("D4");
            HttpContext.Session.SetString("PwReset_Code", code);
            HttpContext.Session.SetString("PwReset_Expiry", DateTime.UtcNow.AddMinutes(10).ToString("o"));
            HttpContext.Session.SetInt32("PwReset_Attempts", 0);

            var sent = await _emailService.TrySendCodeAsync(email, code);
            if (!sent)
            {
                TempData["PwReset_DevCode"] = code;
            }

            StatusMessage = "A new code was sent.";
            return RedirectToPage();
        }

        private void ClearPendingReset()
        {
            HttpContext.Session.Remove("PwReset_EmployeeId");
            HttpContext.Session.Remove("PwReset_Email");
            HttpContext.Session.Remove("PwReset_Code");
            HttpContext.Session.Remove("PwReset_Expiry");
            HttpContext.Session.Remove("PwReset_Attempts");
        }

        private static string MaskEmail(string email)
        {
            var atIndex = email.IndexOf('@');
            if (atIndex <= 1) return email;

            var name = email[..atIndex];
            var domain = email[atIndex..];
            var visible = name[..1];
            return $"{visible}{new string('*', Math.Max(1, name.Length - 1))}{domain}";
        }
    }
}
