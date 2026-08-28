using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Model;
using TM_PE.Data;

namespace TM_PE.Pages.Manager.OfficeTask
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public IList<Model.OfficeTask> OfficeTask { get; set; } = default!;

        // Filters are applied client-side (see Index.cshtml script), these just
        // keep the form fields populated when the page reloads.
        [BindProperty(SupportsGet = true)] public string? Search { get; set; }
        [BindProperty(Name = "status", SupportsGet = true)] public string? StatusFilter { get; set; }

        // Not a column on OfficeTask - filters to tasks that have at least one
        // TaskActivity in the given state. Used by the Dashboard's "Needs Your
        // Attention" panel ("activity=pending-review" / "activity=rejected"),
        // since neither activity review state maps cleanly onto OfficeTask.Status.
        [BindProperty(Name = "activity", SupportsGet = true)] public string? ActivityFilter { get; set; }

        public async Task OnGetAsync()
        {
            OfficeTask = await _context.OfficeTasks
                .Include(t => t.Assignments)
                    .ThenInclude(a => a.Employee)
                .Include(t => t.Activities)
                .Include(t => t.AssignedByEmployee)
                .OrderByDescending(t => t.DateCreated)
                .ToListAsync();

            await RefreshOverdueStatusesAsync();

            if (ActivityFilter == "pending-review")
            {
                OfficeTask = OfficeTask.Where(t => t.Activities.Any(a => a.Status == "Submitted")).ToList();
            }
            else if (ActivityFilter == "rejected")
            {
                OfficeTask = OfficeTask.Where(t => t.Activities.Any(a => a.Status == "Rejected")).ToList();
            }
        }

        // A task becomes Overdue purely because time has passed, not because someone edited
        // it, so re-check on every page load and persist the change if the status flipped.
        private async Task RefreshOverdueStatusesAsync()
        {
            var today = DateTime.Now.Date;
            bool changed = false;

            foreach (var task in OfficeTask)
            {
                if (task.Status != "Completed" && task.DueDate.Date < today)
                {
                    if (task.Status != "Overdue")
                    {
                        task.Status = "Overdue";
                        changed = true;
                    }
                }
                else if (task.Status == "Overdue" && task.DueDate.Date >= today)
                {
                    // Due date was pushed back (e.g. via Edit) so it's no longer overdue;
                    // fall back to Pending and let the next recalculation refine it further.
                    task.Status = "Pending";
                    changed = true;
                }
            }

            if (changed)
            {
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var task = await _context.OfficeTasks.FindAsync(id);

            if (task != null)
            {
                _context.OfficeTasks.Remove(task);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }
    }
}
