using Microsoft.EntityFrameworkCore;
using TM_PE.Model;

namespace TM_PE.Data
{
    // Companion to DueSoonReminderChecker: tells an employee once when one of
    // their own OfficeTasks/JobTickets has actually gone past its due date,
    // instead of only ever warning about it 1-2 days ahead of time. Checked
    // straight off the due date/service completion date rather than each
    // record's own persisted Status="Overdue" flag - that flag is only kept
    // fresh by whichever page happens to load the record (see
    // JobTicketOverdueChecker/RefreshOverdueOfficeTaskStatusesAsync), so it
    // can lag behind the actual due date on any page that doesn't happen to
    // touch that specific record. Same "recompute on read, checked from the
    // notification bell on every page load" style as DueSoonReminderChecker,
    // and the same AnyAsync check stops it from repeating once already sent.
    public static class OverdueReminderChecker
    {
        public static async Task EnsureNotifiedAsync(AppDbContext context, int employeeId)
        {
            var today = DateTime.Now.Date;

            var overdueTasks = await context.TaskAssignments
                .Where(a => a.EmployeeID == employeeId
                    && a.OfficeTask!.Status != "Completed"
                    && a.OfficeTask!.DueDate.Date < today)
                .Select(a => a.OfficeTask!)
                .ToListAsync();

            foreach (var task in overdueTasks)
            {
                var message = $"Task \"{task.TaskName}\" ({task.TaskNumber}) is overdue.";
                await NotifyOnceAsync(context, employeeId, message, $"/OfficeStaff/Details/{task.OfficeTaskID}", "bi-exclamation-triangle");
            }

            var overdueTickets = await context.JobTicketAssignments
                .Where(a => a.EmployeeID == employeeId
                    && a.JobTicket!.Status != JobTicketStatuses.Completed
                    && a.JobTicket!.Status != JobTicketStatuses.Cancelled
                    && a.JobTicket!.DateOfCompletion.HasValue
                    && a.JobTicket!.DateOfCompletion!.Value.Date < today)
                .Select(a => a.JobTicket!)
                .ToListAsync();

            foreach (var ticket in overdueTickets)
            {
                var message = $"Job ticket {ticket.TicketNumber} is overdue.";
                await NotifyOnceAsync(context, employeeId, message, $"/FieldTechnician/Details/{ticket.JobTicketID}", "bi-exclamation-triangle");
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
