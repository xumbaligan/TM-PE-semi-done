using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.OfficeTask
{
    public class DeleteModel : PageModel
    {
        private readonly TM_PE.Data.AppDbContext _context;

        public DeleteModel(TM_PE.Data.AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Model.OfficeTask OfficeTask { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var officetask = await _context.OfficeTasks.FirstOrDefaultAsync(m => m.OfficeTaskID == id);

            if (officetask == null)
            {
                return NotFound();
            }
            else
            {
                OfficeTask = officetask;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var officetask = await _context.OfficeTasks.FindAsync(id);
            if (officetask != null)
            {
                OfficeTask = officetask;
                _context.OfficeTasks.Remove(OfficeTask);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
