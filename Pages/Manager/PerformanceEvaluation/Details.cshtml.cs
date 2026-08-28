using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;

namespace TM_PE.Pages.Manager.PerformanceEvaluation
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;
        public DetailsModel(AppDbContext context) => _context = context;

        public Model.PerformanceEvaluation Evaluation { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var evaluation = await _context.PerformanceEvaluations
                .Include(e => e.Employee)
                .Include(e => e.Results).ThenInclude(r => r.Criteria)
                .FirstOrDefaultAsync(e => e.EvaluationID == id);

            if (evaluation == null) return NotFound();

            Evaluation = evaluation;
            return Page();
        }
    }
}
