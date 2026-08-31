using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.Admin.UserManagement
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        public IndexModel(AppDbContext context) => _context = context;

        public List<Model.UserAccount> Accounts { get; set; } = new();
        [BindProperty(SupportsGet = true)] public string? Search { get; set; }
        [BindProperty(SupportsGet = true)] public string? Role { get; set; }

        public async Task OnGetAsync()
        {
            var q = _context.UserAccounts.Include(a => a.Employee).AsQueryable();
            if (!string.IsNullOrWhiteSpace(Search))
                q = q.Where(a => a.Username.Contains(Search) || (a.Employee != null && a.Employee.FullName.Contains(Search)));
            if (Enum.TryParse<RoleType>(Role, out var rt))
                q = q.Where(a => a.Employee != null && a.Employee.RoleType == rt);
            Accounts = await q.OrderBy(a => a.Username).ToListAsync();
        }
    }
}