using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;

namespace TM_PE.Pages.Admin.UserManagement
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        public IndexModel(AppDbContext context) => _context = context;

        public List<Model.UserAccount> Accounts { get; set; } = new();

        public async Task OnGetAsync()
        {
            Accounts = await _context.UserAccounts
                .Include(a => a.Employee)
                .OrderBy(a => a.Username)
                .ToListAsync();
        }
    }
}