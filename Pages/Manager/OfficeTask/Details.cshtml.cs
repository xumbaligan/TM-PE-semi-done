using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.Manager.OfficeTask
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

        // Employees not yet assigned to this task, for the "Select an employee to add" dropdown
        public List<Employee> AvailableEmployees { get; set; } = new();

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

            // Keep activities in a stable, predictable order
            task.Activities = task.Activities.OrderBy(a => a.ActivityID).ToList();

            OfficeTask = task;

            var assignedIds = task.Assignments.Select(a => a.EmployeeID).ToList();
            AvailableEmployees = await _context.Employees
                .Where(e => e.IsActive && !assignedIds.Contains(e.EmployeeId))
                .OrderBy(e => e.FullName)
                .ToListAsync();

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
        // ASSIGNED EMPLOYEES
        // ---------------------------------------------------------------
        public async Task<IActionResult> OnPostAddEmployeeAsync(int officeTaskId, int employeeId)
        {
            if (employeeId > 0)
            {
                bool alreadyAssigned = await _context.TaskAssignments
                    .AnyAsync(a => a.OfficeTaskID == officeTaskId && a.EmployeeID == employeeId);

                if (!alreadyAssigned)
                {
                    _context.TaskAssignments.Add(new TaskAssignment
                    {
                        OfficeTaskID = officeTaskId,
                        EmployeeID = employeeId,
                        AssignedDate = DateTime.Now
                    });
                    await _context.SaveChangesAsync();
                }
            }

            return RedirectToPage(new { id = officeTaskId });
        }

        public async Task<IActionResult> OnPostRemoveEmployeeAsync(int taskAssignmentId, int officeTaskId)
        {
            var assignment = await _context.TaskAssignments.FindAsync(taskAssignmentId);
            if (assignment != null)
            {
                _context.TaskAssignments.Remove(assignment);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage(new { id = officeTaskId });
        }

        // ---------------------------------------------------------------
        // ACTIVITIES
        // ---------------------------------------------------------------
        public async Task<IActionResult> OnPostAddActivityAsync(int officeTaskId, string activityName)
        {
            if (!string.IsNullOrWhiteSpace(activityName))
            {
                _context.TaskActivities.Add(new TaskActivity
                {
                    OfficeTaskID = officeTaskId,
                    ActivityName = activityName.Trim(),
                    Status = "Pending",
                    DateCreated = DateTime.Now
                });
                await _context.SaveChangesAsync();
                await RecalculateTaskAsync(officeTaskId);
            }

            return RedirectToPage(new { id = officeTaskId });
        }

        public async Task<IActionResult> OnPostRemoveActivityAsync(int activityId, int officeTaskId)
        {
            // Remove any submissions tied to this activity first to avoid FK conflicts
            var submissions = _context.ActivitySubmissions.Where(s => s.ActivityID == activityId);
            _context.ActivitySubmissions.RemoveRange(submissions);

            var activity = await _context.TaskActivities.FindAsync(activityId);
            if (activity != null)
            {
                _context.TaskActivities.Remove(activity);
            }

            await _context.SaveChangesAsync();
            await RecalculateTaskAsync(officeTaskId);

            return RedirectToPage(new { id = officeTaskId });
        }

        // ---------------------------------------------------------------
        // SAVE / CANCEL
        // ---------------------------------------------------------------
        // Approve/Reject (see Details.cshtml) only stage a status client-side -
        // nothing about this task is written to the database until the manager
        // actually clicks Save, which posts everything (every row's staged
        // status and feedback text) in one shot. Cancel never posts at all, so
        // clicking it discards every staged change and leaves the task exactly
        // as it was.
        public async Task<IActionResult> OnPostSaveAsync(int officeTaskId, List<int>? activityIds, List<string>? statuses, List<string>? feedbacks)
        {
            activityIds ??= new();
            statuses ??= new();
            feedbacks ??= new();

            // Which activities currently have a submitted file, so Approve/Reject can be blocked without one.
            var activityIdsWithSubmission = (await _context.ActivitySubmissions
                .Where(s => activityIds.Contains(s.ActivityID))
                .Select(s => s.ActivityID)
                .Distinct()
                .ToListAsync())
                .ToHashSet();

            var errors = new List<string>();

            for (int i = 0; i < activityIds.Count; i++)
            {
                var activity = await _context.TaskActivities.FindAsync(activityIds[i]);
                if (activity == null)
                {
                    continue;
                }

                // Once an activity has been Approved, it is locked: no further status or
                // feedback changes are allowed, even if the posted form tries to change it.
                if (activity.Status == "Approved")
                {
                    continue;
                }

                var newStatus = i < statuses.Count ? statuses[i] : null;
                var newFeedback = i < feedbacks.Count ? feedbacks[i]?.Trim() ?? string.Empty : activity.FeedBack;

                if (!string.IsNullOrWhiteSpace(newStatus) && (newStatus == "Approved" || newStatus == "Rejected"))
                {
                    if (!activityIdsWithSubmission.Contains(activity.ActivityID))
                    {
                        errors.Add($"\"{activity.ActivityName}\" cannot be {newStatus.ToLower()} until the employee attaches a file.");
                        continue;
                    }

                    if (newStatus == "Rejected" && string.IsNullOrWhiteSpace(newFeedback))
                    {
                        errors.Add($"\"{activity.ActivityName}\" was rejected, so feedback is required.");
                        continue;
                    }
                }
            }

            if (errors.Any())
            {
                ErrorMessage = string.Join(" ", errors);
                return RedirectToPage(new { id = officeTaskId });
            }

            // Whoever is logged in as the reviewing manager - recorded against
            // whichever attempt gets approved/rejected below.
            var reviewerEmployeeId = HttpContext.Session.GetInt32("AuthEmployeeId");

            for (int i = 0; i < activityIds.Count; i++)
            {
                var activity = await _context.TaskActivities.FindAsync(activityIds[i]);
                if (activity == null)
                {
                    continue;
                }

                // Approved activities are locked and can never be changed again.
                if (activity.Status == "Approved")
                {
                    continue;
                }

                var newFeedback = i < feedbacks.Count ? feedbacks[i]?.Trim() ?? string.Empty : null;

                // The manager can only Approve or Reject - never set an activity to any
                // other status by hand, even via a hand-crafted POST.
                if (i < statuses.Count && (statuses[i] == "Approved" || statuses[i] == "Rejected"))
                {
                    // Only counts as a fresh rejection if it's actually transitioning
                    // into Rejected now - re-saving an already-Rejected activity
                    // (e.g. just editing its feedback) must not inflate the count.
                    if (statuses[i] == "Rejected" && activity.Status != "Rejected")
                    {
                        activity.RejectionCount++;
                    }

                    activity.Status = statuses[i];

                    // Record the outcome against the specific attempt being
                    // reviewed - its own file stays tied to its own
                    // feedback/reviewer/reviewed-date permanently, instead of
                    // only ever living on the activity's single
                    // current-feedback field (which a later re-upload clears).
                    var attemptBeingReviewed = await _context.ActivitySubmissions
                        .Where(s => s.ActivityID == activity.ActivityID)
                        .OrderByDescending(s => s.DateSubmitted)
                        .FirstOrDefaultAsync();
                    if (attemptBeingReviewed != null)
                    {
                        attemptBeingReviewed.Status = statuses[i];
                        attemptBeingReviewed.Feedback = string.IsNullOrWhiteSpace(newFeedback) ? null : newFeedback;
                        attemptBeingReviewed.ReviewedByEmployeeID = reviewerEmployeeId;
                        attemptBeingReviewed.DateReviewed = DateTime.Now;
                    }
                }

                if (i < feedbacks.Count)
                {
                    activity.FeedBack = feedbacks[i]?.Trim() ?? string.Empty;
                }
            }

            await _context.SaveChangesAsync();
            await RecalculateTaskAsync(officeTaskId);

            return RedirectToPage("./Index");
        }

        // ---------------------------------------------------------------
        // SUBMISSIONS (file upload / download)
        // ---------------------------------------------------------------
        public async Task<IActionResult> OnPostUploadSubmissionAsync(int activityId, int officeTaskId, IFormFile submissionFile)
        {
            if (submissionFile == null || submissionFile.Length == 0)
            {
                ErrorMessage = "Please choose a file to upload.";
                return RedirectToPage(new { id = officeTaskId });
            }

            // Attribute the upload to the first employee assigned to this task.
            var firstAssignment = await _context.TaskAssignments
                .Where(a => a.OfficeTaskID == officeTaskId)
                .OrderBy(a => a.AssignedDate)
                .FirstOrDefaultAsync();

            if (firstAssignment == null)
            {
                ErrorMessage = "Assign an employee to this task before uploading a submission.";
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

            // One submission record per activity: replace the previous file if one exists
            var existing = await _context.ActivitySubmissions
                .FirstOrDefaultAsync(s => s.ActivityID == activityId);

            if (existing != null)
            {
                existing.FileName = submissionFile.FileName;
                existing.FilePath = relativePath;
                existing.DateSubmitted = DateTime.Now;
                existing.Status = "Pending Review";
                existing.EmployeeID = firstAssignment.EmployeeID;
            }
            else
            {
                _context.ActivitySubmissions.Add(new ActivitySubmission
                {
                    ActivityID = activityId,
                    EmployeeID = firstAssignment.EmployeeID,
                    FileName = submissionFile.FileName,
                    FilePath = relativePath,
                    DateSubmitted = DateTime.Now,
                    Status = "Pending Review"
                });
            }

            // A fresh upload puts the activity back into review, unless a manager already approved it.
            var activity = await _context.TaskActivities.FindAsync(activityId);
            if (activity != null && activity.Status != "Approved")
            {
                activity.Status = "Submitted";
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
        // Status/Progress auto-calculation
        // ---------------------------------------------------------------
        // Rule: Progress = % of activities that are Approved.
        //       Status = Completed  -> every activity is Approved
        //                In Progress -> at least one activity is Approved or Submitted
        //                Pending    -> otherwise (or no activities yet)
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

                // Score: 100 points split evenly across all activities, earned per Approved activity.
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
                    // rejecting the only Submitted/Approved activity leaves nothing
                    // currently qualifying - it should never fall back to Pending.
                    task.Status = "Pending";
                }
            }

            // A task that isn't finished and is past its due date is Overdue, regardless of
            // the status computed above.
            if (task.Status != "Completed" && task.DueDate.Date < DateTime.Now.Date)
            {
                task.Status = "Overdue";
            }

            await _context.SaveChangesAsync();
        }
    }
}
