using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.OfficeStaff
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public DetailsModel(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public TM_PE.Model.OfficeTask OfficeTask { get; set; } = default!;

        public Employee CurrentEmployee { get; set; } = default!;

        // Latest submission per ActivityID (for the Submission column)
        public Dictionary<int, ActivitySubmission> LatestSubmissions { get; set; } = new();

        // Every attempt ever made per ActivityID, newest first - for the
        // "History of Submission" modal (see Pages/Shared/_ActivitySubmissionHistoryModal.cshtml).
        public Dictionary<int, List<ActivitySubmission>> SubmissionHistory { get; set; } = new();

        [TempData]
        public string? ErrorMessage { get; set; }

        // ---------------------------------------------------------------
        // LOAD
        // ---------------------------------------------------------------
        public async Task<IActionResult> OnGetAsync(int? id)
        {
            var employeeId = HttpContext.Session.GetInt32("CurrentEmployeeId");
            if (employeeId == null)
            {
                return RedirectToPage("./Select");
            }

            var employee = await _context.Employees.FindAsync(employeeId.Value);
            if (employee == null)
            {
                HttpContext.Session.Remove("CurrentEmployeeId");
                return RedirectToPage("./Select");
            }

            CurrentEmployee = employee;

            if (id == null)
            {
                return NotFound();
            }

            var task = await _context.OfficeTasks
                .Include(t => t.Assignments).ThenInclude(a => a.Employee)
                .Include(t => t.Activities).ThenInclude(a => a.AssignedEmployee)
                .Include(t => t.AssignedByEmployee)
                .FirstOrDefaultAsync(t => t.OfficeTaskID == id);

            if (task == null)
            {
                return NotFound();
            }

            bool isAssignedToTask = task.Assignments.Any(a => a.EmployeeID == employeeId.Value);
            if (!isAssignedToTask)
            {
                ErrorMessage = "You are not assigned to that task.";
                return RedirectToPage("./Index");
            }

            task.Activities = task.Activities.OrderBy(a => a.ActivityID).ToList();
            OfficeTask = task;

            var activityIds = task.Activities.Select(a => a.ActivityID).ToList();
            var allSubmissions = await _context.ActivitySubmissions
                .Include(s => s.Employee)
                .Include(s => s.ReviewedByEmployee)
                .Where(s => activityIds.Contains(s.ActivityID))
                .OrderByDescending(s => s.DateSubmitted)
                .ToListAsync();

            LatestSubmissions = allSubmissions
                .GroupBy(s => s.ActivityID)
                .ToDictionary(g => g.Key, g => g.First());

            SubmissionHistory = allSubmissions
                .GroupBy(s => s.ActivityID)
                .ToDictionary(g => g.Key, g => g.ToList());

            return Page();
        }

        // ---------------------------------------------------------------
        // SUBMISSIONS (file upload / download)
        // ---------------------------------------------------------------
        // Employees can only upload for activities they're allowed to work on:
        //   - Tasks with a single assigned employee: that employee can always upload
        //     (until the activity is Approved).
        //   - Tasks with multiple assigned employees: activities are no longer pre-assigned.
        //     Whoever uploads first "claims" the activity. After that, only the claiming
        //     employee can upload again; everyone else can still see the file but not
        //     replace it. Nobody can upload once the activity is Approved.
        public async Task<IActionResult> OnPostUploadSubmissionAsync(int activityId, int officeTaskId, IFormFile submissionFile)
        {
            var employeeId = HttpContext.Session.GetInt32("CurrentEmployeeId");
            if (employeeId == null)
            {
                return RedirectToPage("./Select");
            }

            var task = await _context.OfficeTasks
                .Include(t => t.Assignments)
                .FirstOrDefaultAsync(t => t.OfficeTaskID == officeTaskId);

            if (task == null)
            {
                return NotFound();
            }

            bool isAssignedToTask = task.Assignments.Any(a => a.EmployeeID == employeeId.Value);
            if (!isAssignedToTask)
            {
                ErrorMessage = "You are not assigned to that task.";
                return RedirectToPage(new { id = officeTaskId });
            }

            var activity = await _context.TaskActivities.FindAsync(activityId);
            if (activity == null || activity.OfficeTaskID != officeTaskId)
            {
                return NotFound();
            }

            if (activity.Status == "Approved")
            {
                ErrorMessage = "This activity has already been approved and can no longer be changed.";
                return RedirectToPage(new { id = officeTaskId });
            }

            bool soleAssignee = task.Assignments.Count == 1;

            if (!soleAssignee)
            {
                // Someone else already claimed this activity by uploading first.
                if (activity.AssignedEmployeeID.HasValue && activity.AssignedEmployeeID.Value != employeeId.Value)
                {
                    ErrorMessage = "Another assigned employee has already uploaded a file for this activity. You can still view it, but you can't upload your own.";
                    return RedirectToPage(new { id = officeTaskId });
                }
            }

            if (submissionFile == null || submissionFile.Length == 0)
            {
                ErrorMessage = "Please choose a file to upload.";
                return RedirectToPage(new { id = officeTaskId });
            }

            var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads", "activity-submissions");
            Directory.CreateDirectory(uploadsRoot);

            var safeFileName = $"{activityId}_{Guid.NewGuid():N}_{Path.GetFileName(submissionFile.FileName)}";
            var fullPath = Path.Combine(uploadsRoot, safeFileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await submissionFile.CopyToAsync(stream);
            }

            var relativePath = Path.Combine("uploads", "activity-submissions", safeFileName).Replace("\\", "/");

            // Every upload is its own attempt, kept forever - never overwrite a
            // previous submission. Before this, re-uploading replaced the one
            // row in place, silently erasing the prior file and whatever
            // feedback/reviewer/reviewed-date the manager had already recorded
            // against it.
            _context.ActivitySubmissions.Add(new ActivitySubmission
            {
                ActivityID = activityId,
                EmployeeID = employeeId.Value,
                FileName = submissionFile.FileName,
                FilePath = relativePath,
                DateSubmitted = DateTime.Now,
                Status = "Pending Review"
            });

            // The uploader claims the activity (a no-op for tasks with only one assignee,
            // who already owns every activity).
            activity.AssignedEmployeeID = employeeId.Value;

            // A fresh upload puts the activity back into review, unless a manager already approved it.
            // If it was Rejected, the manager's feedback applied to the old file, so clear it —
            // it shouldn't linger once the employee has addressed it with a new submission.
            if (activity.Status != "Approved")
            {
                if (activity.Status == "Rejected")
                {
                    activity.FeedBack = string.Empty;
                }

                activity.Status = "Submitted";
            }

            await _context.SaveChangesAsync();
            await RecalculateTaskAsync(officeTaskId);

            return RedirectToPage(new { id = officeTaskId });
        }

        // Lets the employee take back a file they uploaded by mistake, as long
        // as no manager has reviewed it yet - once it's Approved or Rejected,
        // that decision (and any feedback) is permanent history and can no
        // longer be undone.
        public async Task<IActionResult> OnPostRemoveSubmissionAsync(int submissionId, int officeTaskId)
        {
            var employeeId = HttpContext.Session.GetInt32("CurrentEmployeeId");
            if (employeeId == null)
            {
                return RedirectToPage("./Select");
            }

            var task = await _context.OfficeTasks
                .Include(t => t.Assignments)
                .FirstOrDefaultAsync(t => t.OfficeTaskID == officeTaskId);

            if (task == null)
            {
                return NotFound();
            }

            bool isAssignedToTask = task.Assignments.Any(a => a.EmployeeID == employeeId.Value);
            if (!isAssignedToTask)
            {
                ErrorMessage = "You are not assigned to that task.";
                return RedirectToPage(new { id = officeTaskId });
            }

            var submission = await _context.ActivitySubmissions
                .Include(s => s.Activity)
                .FirstOrDefaultAsync(s => s.SubmissionID == submissionId);

            if (submission == null || submission.Activity == null || submission.Activity.OfficeTaskID != officeTaskId)
            {
                return NotFound();
            }

            if (submission.Status != "Pending Review")
            {
                ErrorMessage = "This file has already been reviewed and can no longer be removed.";
                return RedirectToPage(new { id = officeTaskId });
            }

            bool soleAssignee = task.Assignments.Count == 1;
            var activity = submission.Activity;

            if (!soleAssignee && activity.AssignedEmployeeID.HasValue && activity.AssignedEmployeeID.Value != employeeId.Value)
            {
                ErrorMessage = "You can only remove a file you uploaded yourself.";
                return RedirectToPage(new { id = officeTaskId });
            }

            _context.ActivitySubmissions.Remove(submission);
            await _context.SaveChangesAsync();

            // Fall back to whatever the previous attempt looked like, if there
            // was one, instead of assuming the activity is now brand new - e.g.
            // removing a fresh re-upload should put a previously Rejected
            // activity right back to "waiting re-upload", not silently erase
            // that it was ever rejected.
            var priorAttempt = await _context.ActivitySubmissions
                .Where(s => s.ActivityID == activity.ActivityID)
                .OrderByDescending(s => s.DateSubmitted)
                .FirstOrDefaultAsync();

            if (priorAttempt != null)
            {
                activity.Status = priorAttempt.Status;
                activity.FeedBack = priorAttempt.Feedback ?? string.Empty;
            }
            else
            {
                activity.Status = "Pending";
                activity.AssignedEmployeeID = null;
                activity.FeedBack = string.Empty;
            }

            await _context.SaveChangesAsync();
            await RecalculateTaskAsync(officeTaskId);

            return RedirectToPage(new { id = officeTaskId });
        }

        public async Task<IActionResult> OnGetDownloadAsync(int submissionId)
        {
            var submission = await _context.ActivitySubmissions.FindAsync(submissionId);
            if (submission == null || string.IsNullOrEmpty(submission.FilePath))
            {
                return NotFound();
            }

            var fullPath = Path.Combine(_env.WebRootPath, submission.FilePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound();
            }

            var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            return File(bytes, "application/octet-stream", submission.FileName);
        }

        // ---------------------------------------------------------------
        // Status/Progress auto-calculation (mirrors the manager-side rule)
        // ---------------------------------------------------------------
        private async Task RecalculateTaskAsync(int officeTaskId)
        {
            var task = await _context.OfficeTasks
                .Include(t => t.Activities)
                .FirstOrDefaultAsync(t => t.OfficeTaskID == officeTaskId);

            if (task == null)
            {
                return;
            }

            var activities = task.Activities.ToList();

            if (activities.Count == 0)
            {
                task.Status = "Pending";
                task.Progress = 0;
                task.Score = 0;
            }
            else
            {
                int approvedCount = activities.Count(a => a.Status == "Approved");
                task.Progress = Math.Round((decimal)approvedCount / activities.Count * 100, 0);

                decimal pointsPerActivity = 100m / activities.Count;
                task.Score = Math.Round(pointsPerActivity * approvedCount, 2);

                if (approvedCount == activities.Count)
                {
                    if (task.Status != "Completed") task.DateCompleted = DateTime.Now;
                    task.Status = "Completed";
                }
                else if (activities.Any(a => a.Status == "Approved" || a.Status == "Submitted"))
                {
                    task.Status = "In Progress";
                }
                else if (task.Status != "In Progress")
                {
                    // Once a task has been marked In Progress, it stays there even if
                    // a Rejected activity leaves nothing currently Submitted/Approved -
                    // it should never fall back to Pending.
                    task.Status = "Pending";
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
