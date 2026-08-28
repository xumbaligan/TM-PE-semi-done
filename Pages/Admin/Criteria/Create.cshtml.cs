using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;
using CriteriaModel = TM_PE.Model.Criteria;

namespace TM_PE.Pages.Admin.Criteria;

public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    public CreateModel(AppDbContext db) => _db = db;

    [BindProperty] public CriteriaModel Item { get; set; } = new() { IsActive = true };

    // Weight already committed to active criteria for each Role Type, so the
    // Create form can show how much of the 100% is still available before
    // CriteriaValidation would reject the save.
    public decimal OfficeStaffWeightUsed { get; set; }
    public decimal FieldTechnicianWeightUsed { get; set; }

    public async Task OnGetAsync()
    {
        OfficeStaffWeightUsed = await _db.Criteria
            .Where(c => c.RoleType == RoleType.OfficeStaff && c.IsActive)
            .SumAsync(c => (decimal?)c.Weight) ?? 0;
        FieldTechnicianWeightUsed = await _db.Criteria
            .Where(c => c.RoleType == RoleType.FieldTechnician && c.IsActive)
            .SumAsync(c => (decimal?)c.Weight) ?? 0;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await OnGetAsync();
            return Page();
        }

        var error = await CriteriaValidation.ValidateAsync(_db, Item, excludingId: null);
        if (error != null)
        {
            ModelState.AddModelError(string.Empty, error);
            await OnGetAsync();
            return Page();
        }

        _db.Criteria.Add(Item);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
