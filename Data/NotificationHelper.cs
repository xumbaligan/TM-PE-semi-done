using TM_PE.Model;

namespace TM_PE.Data
{
    // Queues a notification for an employee. This only Adds to the context -
    // it never calls SaveChangesAsync() itself - so a notification is only
    // ever persisted as part of the same SaveChangesAsync() call as the event
    // that triggered it, and never left half-committed if that event fails.
    public static class NotificationHelper
    {
        public static void Notify(AppDbContext context, int? employeeId, string message, string? url = null, string icon = "bi-bell")
        {
            if (employeeId == null || employeeId.Value <= 0 || string.IsNullOrWhiteSpace(message)) return;

            context.Notifications.Add(new Notification
            {
                EmployeeID = employeeId.Value,
                Message = message,
                Url = url,
                Icon = icon,
                DateCreated = DateTime.Now
            });
        }
    }
}
