using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.Manager.JobTickets
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public IList<Model.JobTicket> JobTicket { get; set; } = default!;

        // Filters are applied client-side (see Index.cshtml script), these just
        // keep the form fields populated when the page reloads.
        [BindProperty(SupportsGet = true)] public string? Search { get; set; }
        [BindProperty(Name = "status", SupportsGet = true)] public string? StatusFilter { get; set; }

        public async Task OnGetAsync()
        {
            JobTicket = await _context.JobTickets
                .Include(t => t.Assignments)
                    .ThenInclude(a => a.Employee)
                .Include(t => t.Submissions)
                .Include(t => t.RescheduleHistory)
                .Include(t => t.AssignedByEmployee)
                .OrderByDescending(t => t.DateCreated)
                .ToListAsync();

            // Overdue is purely time-based, so re-check it every time the list is
            // loaded rather than relying on whatever was last saved.
            await JobTicketOverdueChecker.RefreshAsync(_context, JobTicket);

            foreach (var ticket in JobTicket)
            {
                ticket.RescheduleHistory = ticket.RescheduleHistory
                    .OrderByDescending(h => h.DateChanged)
                    .ToList();
            }
        }
    }
}
