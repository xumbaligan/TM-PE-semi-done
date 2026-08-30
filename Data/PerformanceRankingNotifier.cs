using Microsoft.EntityFrameworkCore;
using TM_PE.Model;

namespace TM_PE.Data
{
    // After an evaluation is finalized, tells the current top and bottom
    // scorer among that same role's finalized evaluations for the same
    // period - so a standout or a struggling result doesn't sit unnoticed
    // in a report nobody opened. Call this only after the triggering
    // evaluation itself has been saved (its OverallScore needs to already be
    // in the database for the ranking query below to see it).
    public static class PerformanceRankingNotifier
    {
        public static async Task NotifyTopAndBottomAsync(
            AppDbContext context, int periodMonth, int periodYear, RoleType roleType)
        {
            var finalized = await context.PerformanceEvaluations
                .Where(e => e.EvaluationStatus == EvaluationStatus.Finalized
                    && e.EvaluationPeriodMonth == periodMonth
                    && e.EvaluationPeriodYear == periodYear
                    && e.Employee != null && e.Employee.RoleType == roleType)
                .ToListAsync();

            // Meaningless with fewer than two people, or when everyone tied.
            if (finalized.Count < 2) return;

            var maxScore = finalized.Max(e => e.OverallScore);
            var minScore = finalized.Min(e => e.OverallScore);
            if (maxScore == minScore) return;

            var period = new DateTime(periodYear, periodMonth, 1).ToString("MMMM yyyy");
            var roleLabel = roleType == RoleType.FieldTechnician ? "Field Technicians" : "Office Staff";
            var recordsUrl = roleType == RoleType.FieldTechnician
                ? "/FieldTechnician/PerformanceRecords"
                : "/OfficeStaff/PerformanceRecords";

            foreach (var evaluation in finalized.Where(e => e.OverallScore == maxScore))
            {
                var message = $"You have the highest performance score among {roleLabel} for {period}.";
                await NotifyOnceAsync(context, evaluation.EmployeeID, message, recordsUrl, "bi-trophy");
            }

            foreach (var evaluation in finalized.Where(e => e.OverallScore == minScore))
            {
                var message = $"Your performance score was the lowest among {roleLabel} for {period}. Check your evaluation feedback.";
                await NotifyOnceAsync(context, evaluation.EmployeeID, message, recordsUrl, "bi-graph-down");
            }

            await context.SaveChangesAsync();
        }

        private static async Task NotifyOnceAsync(
            AppDbContext context, int employeeId, string message, string url, string icon)
        {
            // Skips a re-finalize (or a later finalize in the same period)
            // that would otherwise send the same employee the identical
            // headline again.
            bool alreadySent = await context.Notifications
                .AnyAsync(n => n.EmployeeID == employeeId && n.Message == message);
            if (alreadySent) return;

            NotificationHelper.Notify(context, employeeId, message, url, icon);
        }
    }
}
