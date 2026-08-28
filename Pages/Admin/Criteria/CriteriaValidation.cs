using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;
using CriteriaModel = TM_PE.Model.Criteria;

namespace TM_PE.Pages.Admin.Criteria;

// Shared by Create/Edit (Admin and, if it ever grows its own forms, Manager
// too). Every criterion is rated by hand, and its weight feeds into the same
// Overall Score, so the active set for either role type can never add up to
// more than the 100% the Overall Score is out of.
public static class CriteriaValidation
{
    // Pass excludingId when editing an existing criterion so it doesn't
    // collide with its own prior values.
    public static async Task<string?> ValidateAsync(
        AppDbContext db, CriteriaModel item, int? excludingId)
    {
        var name = (item.CriteriaName ?? string.Empty).Trim();

        // Names double as how managers tell criteria apart, so no two
        // criteria - active or not - may share one.
        var duplicateName = await db.Criteria
            .Where(c => c.CriteriaName.ToLower() == name.ToLower()
                && (excludingId == null || c.CriteriaId != excludingId.Value))
            .AnyAsync();
        if (duplicateName)
        {
            return $"A criterion named \"{name}\" already exists. Please choose a different name.";
        }

        // A criterion with no weight would never count toward anyone's
        // Overall Score, so it can't be saved.
        if (item.Weight <= 0)
        {
            return "Please set a weight greater than 0 - a criterion can't be saved without one.";
        }

        if (!item.IsActive
            || (item.RoleType != RoleType.FieldTechnician && item.RoleType != RoleType.OfficeStaff))
        {
            return null;
        }

        var others = await db.Criteria
            .Where(c => c.RoleType == item.RoleType
                && c.IsActive
                && (excludingId == null || c.CriteriaId != excludingId.Value))
            .ToListAsync();

        var totalWeight = others.Sum(c => c.Weight) + item.Weight;
        if (totalWeight > 100)
        {
            var roleLabel = item.RoleType == RoleType.FieldTechnician ? "Field Technician" : "Office Staff";
            return $"Active {roleLabel} criteria would total {totalWeight.ToString("0.##")}% weight, which is over 100%. " +
                   $"Lower this criterion's weight, or deactivate another {roleLabel} criterion first.";
        }

        return null;
    }
}
