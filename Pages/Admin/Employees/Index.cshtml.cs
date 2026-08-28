using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.Admin.Employees;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<Employee> Employees { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Role { get; set; }

    public async Task OnGetAsync()
    {
        var q = _db.Employees.Include(e => e.Department).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Search))
            q = q.Where(e => e.FullName.Contains(Search) || e.Email.Contains(Search));
        if (Enum.TryParse<RoleType>(Role, out var rt))
            q = q.Where(e => e.RoleType == rt);
        Employees = await q.OrderBy(e => e.FullName).ToListAsync();
    }
}
