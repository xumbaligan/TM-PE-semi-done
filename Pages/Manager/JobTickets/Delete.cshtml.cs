using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.Manager.JobTickets
{
    public class DeleteModel : PageModel
    {
        private readonly TM_PE.Data.AppDbContext _context;

        public DeleteModel(TM_PE.Data.AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public JobTicket JobTicket { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var jobticket = await _context.JobTickets.FirstOrDefaultAsync(m => m.JobTicketID == id);

            if (jobticket == null)
            {
                return NotFound();
            }
            else
            {
                JobTicket = jobticket;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var jobticket = await _context.JobTickets.FindAsync(id);
            if (jobticket != null)
            {
                JobTicket = jobticket;
                _context.JobTickets.Remove(JobTicket);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
