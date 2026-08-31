using Microsoft.EntityFrameworkCore;
using TM_PE.Model;

namespace TM_PE.Data
{
    // Tells an Admin, once, about every active employee who still has no
    // login account - otherwise that gap only ever surfaces if an Admin
    // happens to notice a name missing from User Management. Same
    // "recompute on read, checked from the notification bell, dedup by
    // message" style as DueSoonReminderChecker/OverdueReminderChecker.
    public static class NoAccountReminderChecker
    {
        public static async Task EnsureNotifiedAsync(AppDbContext context, int adminEmployeeId)
        {
            var employeeIdsWithAccounts = await context.UserAccounts
                .Select(a => a.EmployeeID)
                .ToListAsync();

            var employeesWithoutAccounts = await context.Employees
                .Where(e => e.IsActive && !employeeIdsWithAccounts.Contains(e.EmployeeId))
                .ToListAsync();

            foreach (var employee in employeesWithoutAccounts)
            {
                var message = $"{employee.FullName} has no user account yet.";

                bool alreadySent = await context.Notifications
                    .AnyAsync(n => n.EmployeeID == adminEmployeeId && n.Message == message);
                if (alreadySent) continue;

                NotificationHelper.Notify(context, adminEmployeeId, message,
                    "/Admin/UserManagement/Create", "bi-person-x");
            }

            if (context.ChangeTracker.HasChanges())
            {
                await context.SaveChangesAsync();
            }
        }
    }
}
