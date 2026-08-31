using Microsoft.EntityFrameworkCore;

namespace TM_PE.Data
{
    // Tells a Manager, once, that a new monthly evaluation period has opened -
    // otherwise nothing on the Dashboard/notification bell ever prompts them
    // to go start appraising their team for the period that just began. Fires
    // for the first few days of a month rather than only its first day, so a
    // Manager who didn't log in exactly on the 1st still gets reminded. Same
    // "recompute on read, checked from the notification bell, dedup by
    // message" style as DueSoonReminderChecker - the message itself carries
    // the period label, so it naturally stops repeating once the month moves on.
    public static class EvaluationPeriodReminderChecker
    {
        private const int ReminderWindowDays = 3;

        public static async Task EnsureNotifiedAsync(AppDbContext context, int managerEmployeeId)
        {
            var today = DateTime.Now.Date;
            if (today.Day > ReminderWindowDays) return;

            var periodLabel = today.ToString("MMMM yyyy");
            var message = $"The {periodLabel} evaluation period has started. Evaluate your team when ready.";

            bool alreadySent = await context.Notifications
                .AnyAsync(n => n.EmployeeID == managerEmployeeId && n.Message == message);
            if (alreadySent) return;

            NotificationHelper.Notify(context, managerEmployeeId, message,
                "/Manager/PerformanceEvaluation/Create", "bi-calendar-check");
            await context.SaveChangesAsync();
        }
    }
}
