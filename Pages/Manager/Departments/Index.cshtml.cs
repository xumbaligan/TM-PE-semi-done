using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.Manager.Departments;

// View-only: managers can browse departments, but Create/Edit/Delete
// remain Admin-only (see Pages/Admin/Departments).
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<Department> Departments { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }

    public async Task OnGetAsync()
    {
        var q = _db.Departments.Include(d => d.Employees).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Search))
            q = q.Where(d => d.DepartmentName.Contains(Search));
        Departments = await q.OrderBy(d => d.DepartmentName).ToListAsync();
    }
}
