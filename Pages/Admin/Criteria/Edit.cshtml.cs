
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;
using CriteriaModel = TM_PE.Model.Criteria;

namespace TM_PE.Pages.Admin.Criteria;

public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    public EditModel(AppDbContext db) => _db = db;

    [BindProperty] public CriteriaModel Item { get; set; } = new();

    // Weight already committed to other active criteria for each Role Type
    // (excluding this one), so the Edit form can show how much of the 100%
    // is still available before CriteriaValidation would reject the save.
    public decimal OfficeStaffWeightUsed { get; set; }
    public decimal FieldTechnicianWeightUsed { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var c = await _db.Criteria.FindAsync(id);
        if (c == null) return NotFound();
        Item = c;
        await LoadWeightUsedAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadWeightUsedAsync();
            return Page();
        }

        var error = await CriteriaValidation.ValidateAsync(_db, Item, excludingId: Item.CriteriaId);
        if (error != null)
        {
            ModelState.AddModelError(string.Empty, error);
            await LoadWeightUsedAsync();
            return Page();
        }

        _db.Attach(Item).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    private async Task LoadWeightUsedAsync()
    {
        OfficeStaffWeightUsed = await _db.Criteria
            .Where(c => c.RoleType == RoleType.OfficeStaff && c.IsActive && c.CriteriaId != Item.CriteriaId)
            .SumAsync(c => (decimal?)c.Weight) ?? 0;
        FieldTechnicianWeightUsed = await _db.Criteria
            .Where(c => c.RoleType == RoleType.FieldTechnician && c.IsActive && c.CriteriaId != Item.CriteriaId)
            .SumAsync(c => (decimal?)c.Weight) ?? 0;
    }
}
