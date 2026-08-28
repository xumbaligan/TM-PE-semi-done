using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.Manager.PerformanceEvaluation
{
    // Editing an evaluation only ever touches tbl_performanceevaluation and
    // tbl_evaluationresult - it never writes back into JobTicket or OfficeTask.
    // Once an evaluation is Finalized it's locked; only a Draft can be edited.
    public class EditModel : PageModel
    {
        private readonly AppDbContext _context;
        public EditModel(AppDbContext context) => _context = context;

        [BindProperty]
        public Model.PerformanceEvaluation Evaluation { get; set; } = new();

        [BindProperty]
        public List<CreateModel.ResultInput> Results { get; set; } = new();

        public Employee? Employee { get; set; }
        public List<CriteriaStarRow> Rows { get; set; } = new();
        public EmployeePerformanceStats Stats { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var evaluation = await LoadEvaluationAsync(id);
            if (evaluation == null) return NotFound();
            if (evaluation.EvaluationStatus == EvaluationStatus.Finalized)
                return RedirectToPage("Details", new { id });

            Evaluation = evaluation;
            Employee = evaluation.Employee;

            await LoadReferenceDataAsync(evaluation, useExistingScores: true);
            return Page();
        }

        public Task<IActionResult> OnPostSaveDraftAsync(int id) => SaveAsync(id, EvaluationStatus.Draft);

        public Task<IActionResult> OnPostFinalizeAsync(int id) => SaveAsync(id, EvaluationStatus.Finalized);

        private async Task<IActionResult> SaveAsync(int id, EvaluationStatus status)
        {
            var evaluation = await LoadEvaluationAsync(id);
            if (evaluation == null) return NotFound();
            if (evaluation.EvaluationStatus == EvaluationStatus.Finalized)
                return RedirectToPage("Details", new { id });

            Employee = evaluation.Employee;

            ModelState.Remove("Evaluation.Employee");
            ModelState.Remove("Evaluation.EmployeeID");
            ModelState.Remove("Evaluation.OverallScore");
            ModelState.Remove("Evaluation.OverallRating");
            ModelState.Remove("Evaluation.EvaluatorName");
            // The period is fixed once an evaluation is created (see the
            // disabled, unnamed display in Edit.cshtml) - it's never posted,
            // so the bound Evaluation.EvaluationPeriodMonth/Year default to 0
            // and would otherwise fail their [Range] checks below.
            ModelState.Remove("Evaluation.EvaluationPeriodMonth");
            ModelState.Remove("Evaluation.EvaluationPeriodYear");
            ModelState.Remove("Evaluation.EvaluationStatus");

            if (!ModelState.IsValid)
            {
                Evaluation.EvaluationID = id;
                await LoadReferenceDataAsync(evaluation, useExistingScores: false);
                return Page();
            }

            var allowedCriteria = await _context.Criteria
                .Where(c => c.RoleType == evaluation.Employee!.RoleType &&
                            (c.IsActive || evaluation.Results.Select(r => r.CriteriaID).Contains(c.CriteriaId)))
                .ToListAsync();
            var allowedIds = allowedCriteria.ToDictionary(c => c.CriteriaId);

            if (status == EvaluationStatus.Finalized)
            {
                var workQualityError = EvaluationScoring.ValidateAllCriteriaRated(
                    allowedCriteria, Results.Select(r => (r.CriteriaID, r.StarRating, r.Feedback)),
                    requireFeedback: false);
                if (workQualityError != null)
                {
                    ModelState.AddModelError(string.Empty, workQualityError);
                    Evaluation.EvaluationID = id;
                    await LoadReferenceDataAsync(evaluation, useExistingScores: false);
                    return Page();
                }
            }

            evaluation.EvaluationDate = Evaluation.EvaluationDate;
            evaluation.GeneralFeedback = string.IsNullOrWhiteSpace(Evaluation.GeneralFeedback) ? null : Evaluation.GeneralFeedback.Trim();
            evaluation.EvaluationStatus = status;

            _context.EvaluationResults.RemoveRange(evaluation.Results);
            evaluation.Results.Clear();

            decimal overallScore = 0;
            foreach (var r in Results)
            {
                if (!allowedIds.TryGetValue(r.CriteriaID, out var criteria)) continue;

                var stars = EvaluationScoring.NormalizeStars(r.StarRating);
                var score = EvaluationScoring.ScoreFor(stars, criteria.Weight);
                overallScore += score;

                evaluation.Results.Add(new EvaluationResult
                {
                    CriteriaID = criteria.CriteriaId,
                    StarRating = stars,
                    Score = score,
                    Feedback = string.IsNullOrWhiteSpace(r.Feedback) ? null : r.Feedback.Trim()
                });
            }

            evaluation.OverallScore = overallScore;
            evaluation.OverallRating = EvaluationScoring.RatingFor(overallScore);

            await _context.SaveChangesAsync();
            return RedirectToPage("Details", new { id = evaluation.EvaluationID });
        }

        private Task<Model.PerformanceEvaluation?> LoadEvaluationAsync(int id) =>
            _context.PerformanceEvaluations
                .Include(e => e.Employee)
                .Include(e => e.Results)
                .FirstOrDefaultAsync(e => e.EvaluationID == id);

        private async Task LoadReferenceDataAsync(Model.PerformanceEvaluation evaluation, bool useExistingScores)
        {
            if (evaluation.Employee == null) { Rows = new(); return; }

            Stats = await EmployeePerformanceStatsBuilder.BuildForAsync(
                _context, evaluation.EmployeeID, evaluation.EvaluationPeriodStart, evaluation.EvaluationPeriodEnd);

            var existingIds = evaluation.Results.Select(r => r.CriteriaID).ToHashSet();

            // Active criteria for this role type, plus any criteria this
            // evaluation already scored even if since deactivated - so a past
            // score is never silently dropped from the edit screen.
            var criteriaList = await _context.Criteria
                .Where(c => c.RoleType == evaluation.Employee.RoleType &&
                            (c.IsActive || existingIds.Contains(c.CriteriaId)))
                .OrderByDescending(c => c.Weight)
                .ToListAsync();

            Rows = criteriaList.Select(c =>
            {
                if (useExistingScores)
                {
                    var existing = evaluation.Results.FirstOrDefault(r => r.CriteriaID == c.CriteriaId);
                    return new CriteriaStarRow
                    {
                        Criteria = c,
                        StarRating = existing?.StarRating ?? 0,
                        Feedback = existing?.Feedback
                    };
                }

                // Validation failure: keep whatever the manager just posted.
                var posted = Results.FirstOrDefault(r => r.CriteriaID == c.CriteriaId);
                return new CriteriaStarRow
                {
                    Criteria = c,
                    StarRating = posted?.StarRating ?? 0,
                    Feedback = posted?.Feedback
                };
            }).ToList();
        }
    }
}
