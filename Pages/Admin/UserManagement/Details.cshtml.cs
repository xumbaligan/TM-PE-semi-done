using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.Admin.UserManagement
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;
        public DetailsModel(AppDbContext context) => _context = context;

        public UserAccount Account { get; set; } = default!;

        [BindProperty]
        public string NewPassword { get; set; } = string.Empty;

        [BindProperty]
        public string ConfirmNewPassword { get; set; } = string.Empty;

        public string? ResetMessage { get; set; }
        public string? ResetError { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var account = await _context.UserAccounts
                .Include(a => a.Employee).ThenInclude(e => e!.Department)
                .FirstOrDefaultAsync(a => a.UserAccountID == id);

            if (account == null) return NotFound();

            Account = account;
            return Page();
        }

        public async Task<IActionResult> OnPostToggleActiveAsync(int id)
        {
            var account = await _context.UserAccounts.FindAsync(id);
            if (account == null) return NotFound();

            account.IsActive = !account.IsActive;
            await _context.SaveChangesAsync();

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostResetPasswordAsync(int id)
        {
            var account = await _context.UserAccounts
                .Include(a => a.Employee).ThenInclude(e => e!.Department)
                .FirstOrDefaultAsync(a => a.UserAccountID == id);

            if (account == null) return NotFound();
            Account = account;

            if (string.IsNullOrWhiteSpace(NewPassword) ||
                !System.Text.RegularExpressions.Regex.IsMatch(NewPassword, UsernamePolicy.PasswordPattern))
            {
                ResetError = "Password must be 8-10 digits (numbers only).";
                return Page();
            }
            if (NewPassword != ConfirmNewPassword)
            {
                ResetError = "Passwords do not match.";
                return Page();
            }

            var (hash, salt) = PasswordHasher.Hash(NewPassword);
            account.PasswordHash = hash;
            account.PasswordSalt = salt;
            await _context.SaveChangesAsync();

            ResetMessage = "Password reset successfully.";
            return Page();
        }
    }
}