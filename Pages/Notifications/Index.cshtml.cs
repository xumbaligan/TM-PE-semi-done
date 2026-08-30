using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.Notifications
{
    // One shared "All Notifications" page for every role - the bell dropdown
    // (see Pages/Shared/_NotificationBell.cshtml) links here, and its own
    // OnGetPollAsync/OnPostMarkReadAsync/OnPostMarkAllReadAsync handlers are
    // what the bell itself calls via fetch, so opening or reading
    // notifications never triggers a full page navigation.
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        public IndexModel(AppDbContext context) => _context = context;

        public string LayoutName { get; set; } = "_Layout";

        public List<Notification> Items { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var employeeId = HttpContext.Session.GetInt32("AuthEmployeeId");
            if (employeeId == null)
            {
                return RedirectToPage("/Login");
            }

            LayoutName = HttpContext.Session.GetString("AuthRoleType") switch
            {
                "Admin" => "_Admin",
                "FieldTechnician" => "_FieldTechnicianLayout",
                "OfficeStaff" => "_OfficeStaffLayout",
                _ => "_Layout"
            };

            Items = await _context.Notifications
                .Where(n => n.EmployeeID == employeeId.Value)
                .OrderByDescending(n => n.DateCreated)
                .Take(100)
                .ToListAsync();

            return Page();
        }

        // Polled by the bell partial on every page load and on a timer, so the
        // badge count and dropdown list stay current without the visitor ever
        // having to reload or navigate anywhere.
        public async Task<IActionResult> OnGetPollAsync()
        {
            var employeeId = HttpContext.Session.GetInt32("AuthEmployeeId");
            if (employeeId == null)
            {
                return new JsonResult(new { unreadCount = 0, items = Array.Empty<object>() });
            }

            var recent = await _context.Notifications
                .Where(n => n.EmployeeID == employeeId.Value)
                .OrderByDescending(n => n.DateCreated)
                .Take(8)
                .Select(n => new
                {
                    id = n.NotificationID,
                    message = n.Message,
                    url = n.Url,
                    icon = n.Icon,
                    isRead = n.IsRead,
                    dateCreated = n.DateCreated.ToString("MMM d, h:mm tt")
                })
                .ToListAsync();

            var unreadCount = await _context.Notifications
                .CountAsync(n => n.EmployeeID == employeeId.Value && !n.IsRead);

            return new JsonResult(new { unreadCount, items = recent });
        }

        public async Task<IActionResult> OnPostMarkReadAsync(int id)
        {
            var employeeId = HttpContext.Session.GetInt32("AuthEmployeeId");
            if (employeeId == null)
            {
                return new JsonResult(new { success = false }) { StatusCode = 401 };
            }

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationID == id && n.EmployeeID == employeeId.Value);

            if (notification != null && !notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostMarkAllReadAsync()
        {
            var employeeId = HttpContext.Session.GetInt32("AuthEmployeeId");
            if (employeeId == null)
            {
                return new JsonResult(new { success = false }) { StatusCode = 401 };
            }

            var unread = await _context.Notifications
                .Where(n => n.EmployeeID == employeeId.Value && !n.IsRead)
                .ToListAsync();

            foreach (var n in unread)
            {
                n.IsRead = true;
            }

            await _context.SaveChangesAsync();

            return new JsonResult(new { success = true });
        }
    }
}
