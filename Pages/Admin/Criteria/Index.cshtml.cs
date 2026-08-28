using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using CriteriaModel = TM_PE.Model.Criteria;

namespace TM_PE.Pages.Admin.Criteria;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<CriteriaModel> Items { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }

    public async Task OnGetAsync()
    {
        var q = _db.Criteria.AsQueryable();
        if (!string.IsNullOrWhiteSpace(Search))
            q = q.Where(c => c.CriteriaName.Contains(Search));
        Items = await q.OrderBy(c => c.CriteriaName).ToListAsync();
    }


}
