using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.Manager.JobTickets
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public DetailsModel(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public Model.JobTicket JobTicket { get; set; } = default!;

        // The current, not-yet-saved cycle's submissions - i.e. JobTicket.Submissions
        // filtered down for display. Deliberately kept SEPARATE from
        // JobTicket.Submissions itself (never reassign that navigation property to a
        // filtered subset): JobTicketID is a required relationship, so EF Core
        // interprets any submission missing from JobTicket.Submissions after such a
        // reassignment as orphaned and DELETES it outright the next time
        // SaveChangesAsync runs on this context (e.g. from JobTicketOverdueChecker
        // below) - even though it's still validly archived under a SubmissionHistory
        // entry. That silently wiped out every archived submission for a ticket the
        // moment its Details page was reloaded.
        public List<TM_PE.Model.JobTicketSubmission> CurrentSubmissions { get; set; } = new();

        [TempData]
        public string? ErrorMessage { get; set; }

        public static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
        };

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ticket = await _context.JobTickets
                .Include(t => t.Assignments).ThenInclude(a => a.Employee)
                .Include(t => t.Submissions).ThenInclude(s => s.Employee)
                .Include(t => t.RescheduleHistory).ThenInclude(h => h.ArchivedSubmissions)
                .Include(t => t.SubmissionHistory).ThenInclude(h => h.ArchivedSubmissions)
                .Include(t => t.SubmissionHistory).ThenInclude(h => h.ActorEmployee)
                .Include(t => t.AssignedByEmployee)
                .FirstOrDefaultAsync(t => t.JobTicketID == id);

            if (ticket == null)
            {
                return NotFound();
            }

            // Overdue is purely time-based, so re-check it every time the page is
            // opened rather than relying on whatever was last saved.
            await JobTicketOverdueChecker.RefreshAsync(_context, new[] { ticket });

            // Only the current, not-yet-saved cycle's submissions belong here -
            // same filter FieldTechnician/Details uses - since anything archived
            // under a Ticket History or Reschedule History entry already shows
            // there instead. Filtered into CurrentSubmissions rather than
            // reassigned onto ticket.Submissions itself - see that property's
            // doc comment for why.
            CurrentSubmissions = ticket.Submissions
                .Where(s => s.RescheduleHistoryID == null && s.SubmissionHistoryID == null)
                .OrderByDescending(s => s.DateSubmitted)
                .ToList();

            ticket.RescheduleHistory = ticket.RescheduleHistory
                .OrderByDescending(h => h.DateChanged)
                .ToList();

            ticket.SubmissionHistory = ticket.SubmissionHistory
                .OrderByDescending(h => h.DateChanged)
                .ToList();

            JobTicket = ticket;

            return Page();
        }

        public async Task<IActionResult> OnGetDownloadAsync(int submissionId)
        {
            var submission = await _context.JobTicketSubmissions.FindAsync(submissionId);
            if (submission == null || string.IsNullOrEmpty(submission.FilePath))
            {
                return NotFound();
            }

            var fullPath = Path.Combine(_env.WebRootPath, submission.FilePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound();
            }

            var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            return File(bytes, "application/octet-stream", submission.FileName);
        }
    }
}
