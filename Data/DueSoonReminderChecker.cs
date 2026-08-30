using Microsoft.EntityFrameworkCore;
using TM_PE.Model;

namespace TM_PE.Data
{
    // Reminds an employee about their own OfficeTasks/JobTickets that are due
    // soon (1-2 days out) and not yet finished. Checked from the notification
    // bell (see Pages/Shared/_NotificationBell.cshtml) on every page load for
    // the logged-in employee - the same "recompute on read" style already
    // used for JobTicket Overdue status (see JobTicketOverdueChecker) rather
    // than a separate background job. Baking the exact days-left count into
    // the message means a task/ticket naturally gets reminded once at 2 days
    // out and again at 1 day out, but the AnyAsync check below stops it from
    // repeating for the same day no matter how many pages the employee visits.
    public static class DueSoonReminderChecker
    {
        public static async Task EnsureNotifiedAsync(AppDbContext context, int employeeId)
        {
            var today = DateTime.Now.Date;
            var windowStart = today.AddDays(1);
            var windowEnd = today.AddDays(2);

            var dueTasks = await context.TaskAssignments
                .Where(a => a.EmployeeID == employeeId
                    && a.OfficeTask!.Status != "Completed"
                    && a.OfficeTask!.DueDate.Date >= windowStart
                    && a.OfficeTask!.DueDate.Date <= windowEnd)
                .Select(a => a.OfficeTask!)
                .ToListAsync();

            foreach (var task in dueTasks)
            {
                var daysLeft = (task.DueDate.Date - today).Days;
                var message = $"Task \"{task.TaskName}\" ({task.TaskNumber}) is due in {daysLeft} day{(daysLeft == 1 ? "" : "s")}.";
                await NotifyOnceAsync(context, employeeId, message, $"/OfficeStaff/Details/{task.OfficeTaskID}", "bi-alarm");
            }

            var dueTickets = await context.JobTicketAssignments
                .Where(a => a.EmployeeID == employeeId
                    && a.JobTicket!.Status != JobTicketStatuses.Completed
                    && a.JobTicket!.Status != JobTicketStatuses.Cancelled
                    && a.JobTicket!.DateOfCompletion.HasValue
                    && a.JobTicket!.DateOfCompletion!.Value.Date >= windowStart
                    && a.JobTicket!.DateOfCompletion!.Value.Date <= windowEnd)
                .Select(a => a.JobTicket!)
                .ToListAsync();

            foreach (var ticket in dueTickets)
            {
                var daysLeft = (ticket.DateOfCompletion!.Value.Date - today).Days;
                var message = $"Job ticket {ticket.TicketNumber} is due in {daysLeft} day{(daysLeft == 1 ? "" : "s")}.";
                await NotifyOnceAsync(context, employeeId, message, $"/FieldTechnician/Details/{ticket.JobTicketID}", "bi-alarm");
            }

            if (context.ChangeTracker.HasChanges())
            {
                await context.SaveChangesAsync();
            }
        }

        private static async Task NotifyOnceAsync(AppDbContext context, int employeeId, string message, string url, string icon)
        {
            bool alreadySent = await context.Notifications
                .AnyAsync(n => n.EmployeeID == employeeId && n.Message == message);
            if (alreadySent) return;

            NotificationHelper.Notify(context, employeeId, message, url, icon);
        }
    }
}
