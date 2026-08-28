using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.Admin.Departments;

public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    public CreateModel(AppDbContext db) => _db = db;

    [BindProperty] public Department Department { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        _db.Departments.Add(Department);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
