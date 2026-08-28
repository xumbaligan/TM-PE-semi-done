using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Net.Sockets;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.FieldTechnician
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

        public static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
        };

        // "Reschedule Request" can now be picked by the technician leader
        // themselves (in addition to being set directly by a manager). Once set
        // this way, the ticket locks from further field-technician edits until
        // the manager approves/acts on it (see JobTicket.IsLockedFromFieldTechnicianEditing).
        // "Rescheduled" is intentionally NOT offered here — it's never chosen
        // directly by the leader, only ever set by the manager's own reschedule
        // action (and even then, the ticket goes back to Pending — see
        // Manager/JobTickets/Edit.cshtml.cs).
        public static readonly string[] StatusOptions = {
            JobTicketStatuses.Pending,
            JobTicketStatuses.InProgress,
            JobTicketStatuses.Completed,
            JobTicketStatuses.Cancelled,
            JobTicketStatuses.RescheduleRequest
        };

        public JobTicket JobTicket { get; set; } = default!;

        public Employee CurrentEmployee { get; set; } = default!;

        // Only the assignment marked IsLeader for this employee/ticket grants upload
        // and status-change rights; everyone else assigned can view only.
        public bool IsLeader { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        // Unsaved status/remarks carried through from a file upload redirect
        // (see OnPostUploadSubmissionAsync). These are display-only: they are
        // never written to JobTicket/the database, only used to re-populate the
        // Update Status form so an in-progress edit isn't visually lost after an
        // automatic upload postback. Falls back to the saved ticket values when
        // not present (e.g. a normal page load).
        public string? PendingStatus { get; set; }
        public string? PendingRemarks { get; set; }

        // ---------------------------------------------------------------
        // LOAD
        // ---------------------------------------------------------------
        public async Task<IActionResult> OnGetAsync(int? id, string? pendingStatus, string? pendingRemarks)
        {
            var employeeId = HttpContext.Session.GetInt32("CurrentFieldTechnicianId");
            if (employeeId == null)
            {
                return RedirectToPage("./Select");
            }

            var employee = await _context.Employees.FindAsync(employeeId.Value);
            if (employee == null || employee.RoleType != RoleType.FieldTechnician)
            {
                HttpContext.Session.Remove("CurrentFieldTechnicianId");
                return RedirectToPage("./Select");
            }

            CurrentEmployee = employee;

            if (id == null)
            {
                return NotFound();
            }

            var ticket = await _context.JobTickets
                .Include(t => t.Assignments).ThenInclude(a => a.Employee)
                .Include(t => t.Submissions).ThenInclude(s => s.Employee)
                .Include(t => t.SubmissionHistory).ThenInclude(h => h.ArchivedSubmissions).ThenInclude(s => s.Employee)
                .Include(t => t.SubmissionHistory).ThenInclude(h => h.ActorEmployee)
                .Include(t => t.AssignedByEmployee)
                .FirstOrDefaultAsync(t => t.JobTicketID == id);

            if (ticket == null)
            {
                return NotFound();
            }

            var myAssignment = ticket.Assignments.FirstOrDefault(a => a.EmployeeID == employeeId.Value);
            if (myAssignment == null)
            {
                ErrorMessage = "You are not assigned to that job order.";
                return RedirectToPage("./Index");
            }

            // Overdue is purely time-based, so re-check it every time the page is
            // opened rather than relying on whatever was last saved.
            await JobTicketOverdueChecker.RefreshAsync(_context, new[] { ticket });

            IsLeader = myAssignment.IsLeader;

            // Only the current, not-yet-saved cycle's submissions belong in the
            // active list; ones archived under a History of Submission entry (or
            // an older manager-triggered reschedule) show under that history instead.
            ticket.Submissions = ticket.Submissions
                .Where(s => s.RescheduleHistoryID == null && s.SubmissionHistoryID == null)
                .OrderByDescending(s => s.DateSubmitted)
                .ToList();

            ticket.SubmissionHistory = ticket.SubmissionHistory
                .OrderByDescending(h => h.DateChanged)
                .ToList();

            JobTicket = ticket;

            // Only honor pendingStatus/pendingRemarks if they were actually
            // supplied (e.g. redirected here from an upload); otherwise fall
            // back to the ticket's real saved values.
            PendingStatus = !string.IsNullOrWhiteSpace(pendingStatus) && StatusOptions.Contains(pendingStatus)
                ? pendingStatus
                : ticket.Status;

            PendingRemarks = pendingStatus != null
                ? pendingRemarks
                : ticket.Remarks;

            return Page();
        }

        // ---------------------------------------------------------------
        // FILE SUBMISSION (leader only)
        // ---------------------------------------------------------------
        // NOTE: this handler only adds a file — it never changes the ticket's
        // Status or Remarks in the database. Status/remarks changes only ever
        // happen through the explicit "Save" button in OnPostSaveAsync below,
        // so uploading a photo can never silently commit a status change (e.g.
        // auto-completing a ticket) before the leader has actually pressed Save.
        public async Task<IActionResult> OnPostUploadSubmissionAsync(int jobTicketId, IFormFile submissionFile, string? status, string? remarks)
        {
            var employeeId = HttpContext.Session.GetInt32("CurrentFieldTechnicianId");

            if (employeeId == null)
            {
                return RedirectToPage("./Select");
            }

            var ticket = await _context.JobTickets
                .Include(t => t.Assignments)
                .FirstOrDefaultAsync(t => t.JobTicketID == jobTicketId);

            if (ticket == null)
            {
                return NotFound();
            }

            var myAssignment = ticket.Assignments
                .FirstOrDefault(a => a.EmployeeID == employeeId.Value);

            if (myAssignment == null)
            {
                ErrorMessage = "You are not assigned to that job order.";
                return RedirectToPage("./Index");
            }

            if (!myAssignment.IsLeader)
            {
                ErrorMessage =
                    "Only the team leader can upload files for this job order.";

                return RedirectToPage(new { id = jobTicketId });
            }

            if (ticket.IsLockedFromFieldTechnicianEditing)
            {
                ErrorMessage = ticket.Status switch
                {
                    JobTicketStatuses.Completed => "This job order has been marked Completed and can no longer be updated.",
                    JobTicketStatuses.RescheduleRequest => "This job order has a pending reschedule request awaiting manager approval and can't be updated until it's resolved.",
                    _ => "This job order can no longer be updated."
                };

                return RedirectToPage(new { id = jobTicketId });
            }

            if (!ticket.IsAccessibleToFieldTechnician)
            {
                ErrorMessage = $"This job order isn't accessible yet — it's scheduled for {ticket.ServiceDate:M/d/yyyy}.";
                return RedirectToPage(new { id = jobTicketId });
            }

            if (submissionFile == null || submissionFile.Length == 0)
            {
                ErrorMessage = "Please choose a file to upload.";
                return RedirectToPage(new { id = jobTicketId });
            }

            // Deliberately does NOT touch ticket.Status or ticket.Remarks, and
            // never calls SaveChangesAsync() on those fields. The status/remarks
            // the leader currently has selected/typed are only carried through
            // the redirect below (as pendingStatus/pendingRemarks) so the form
            // still shows them after the page reloads; nothing is committed to
            // the database until the leader explicitly presses Save, which posts
            // to OnPostSaveAsync.

            var uploadsRoot = Path.Combine(
                _env.WebRootPath,
                "uploads",
                "jobticket-submissions");

            Directory.CreateDirectory(uploadsRoot);

            var safeFileName =
                $"{jobTicketId}_{Guid.NewGuid():N}_{Path.GetFileName(submissionFile.FileName)}";

            var fullPath = Path.Combine(uploadsRoot, safeFileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await submissionFile.CopyToAsync(stream);
            }

            var relativePath = Path.Combine(
                "uploads",
                "jobticket-submissions",
                safeFileName)
                .Replace("\\", "/");

            _context.JobTicketSubmissions.Add(new JobTicketSubmission
            {
                JobTicketID = jobTicketId,
                EmployeeID = employeeId.Value,
                FileName = submissionFile.FileName,
                FilePath = relativePath,
                DateSubmitted = DateTime.Now
            });

            await _context.SaveChangesAsync();

            return RedirectToPage(new
            {
                id = jobTicketId,
                pendingStatus = status,
                pendingRemarks = remarks
            });
        }

        // ---------------------------------------------------------------
        // REMOVE A SUBMITTED FILE (leader only)
        // ---------------------------------------------------------------
        public async Task<IActionResult> OnPostDeleteSubmissionAsync(int jobTicketId, int submissionId)
        {
            var employeeId = HttpContext.Session.GetInt32("CurrentFieldTechnicianId");
            if (employeeId == null)
            {
                return RedirectToPage("./Select");
            }

            var ticket = await _context.JobTickets
                .Include(t => t.Assignments)
                .FirstOrDefaultAsync(t => t.JobTicketID == jobTicketId);

            if (ticket == null)
            {
                return NotFound();
            }

            var myAssignment = ticket.Assignments.FirstOrDefault(a => a.EmployeeID == employeeId.Value);
            if (myAssignment == null)
            {
                ErrorMessage = "You are not assigned to that job order.";
                return RedirectToPage("./Index");
            }

            if (!myAssignment.IsLeader)
            {
                ErrorMessage = "Only the team leader can remove files for this job order.";
                return RedirectToPage(new { id = jobTicketId });
            }

            if (ticket.IsLockedFromFieldTechnicianEditing)
            {
                ErrorMessage = ticket.Status switch
                {
                    JobTicketStatuses.Completed => "This job order has been marked Completed and can no longer be updated.",
                    JobTicketStatuses.RescheduleRequest => "This job order has a pending reschedule request awaiting manager approval and can't be updated until it's resolved.",
                    _ => "This job order can no longer be updated."
                };
                return RedirectToPage(new { id = jobTicketId });
            }

            if (!ticket.IsAccessibleToFieldTechnician)
            {
                ErrorMessage = $"This job order isn't accessible yet — it's scheduled for {ticket.ServiceDate:M/d/yyyy}.";
                return RedirectToPage(new { id = jobTicketId });
            }

            var submission = await _context.JobTicketSubmissions
                .FirstOrDefaultAsync(s => s.JobTicketSubmissionID == submissionId
                    && s.JobTicketID == jobTicketId
                    && s.RescheduleHistoryID == null
                    && s.SubmissionHistoryID == null);

            if (submission != null)
            {
                var fullPath = Path.Combine(_env.WebRootPath, submission.FilePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }

                _context.JobTicketSubmissions.Remove(submission);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage(new { id = jobTicketId });
        }

        // ---------------------------------------------------------------
        // STATUS + REMARKS (leader only) ? consolidated Save; nothing is
        // persisted until the leader presses Save.
        // ---------------------------------------------------------------
        public async Task<IActionResult> OnPostSaveAsync(int jobTicketId, string status, string? remarks)
        {
            var employeeId = HttpContext.Session.GetInt32("CurrentFieldTechnicianId");

            if (employeeId == null)
            {
                return RedirectToPage("./Select");
            }

            var ticket = await _context.JobTickets
                .Include(t => t.Assignments)
                .Include(t => t.Submissions)
                .FirstOrDefaultAsync(t => t.JobTicketID == jobTicketId);

            if (ticket == null)
            {
                return NotFound();
            }

            var myAssignment = ticket.Assignments
                .FirstOrDefault(a => a.EmployeeID == employeeId.Value);

            if (myAssignment == null)
            {
                ErrorMessage = "You are not assigned to that job order.";
                return RedirectToPage("./Index");
            }

            // Only the leader can change the status
            if (!myAssignment.IsLeader)
            {
                ErrorMessage =
                    "Only the team leader can change the status of this job order.";

                return RedirectToPage(new { id = jobTicketId });
            }

            // Local helper so every validation failure below carries the status
            // and remarks the leader had actually just submitted back through the
            // redirect (as pendingStatus/pendingRemarks) instead of letting the
            // page reload fall back to the ticket's last-saved DB values. Without
            // this, a failed Save (e.g. a missing-file or missing-remarks error)
            // would visibly reset the dropdown/textbox to whatever was saved
            // before, discarding what the leader had just picked/typed.
            IActionResult BackWithError(string message)
            {
                ErrorMessage = message;
                return RedirectToPage(new
                {
                    id = jobTicketId,
                    pendingStatus = status,
                    pendingRemarks = remarks
                });
            }

            // Completed tickets cannot be changed further
            if (ticket.IsLockedFromEditing)
            {
                return BackWithError("This job order has been marked Completed and can no longer be updated.");
            }

            // A pending reschedule request also blocks further changes until the
            // manager acts on it.
            if (ticket.Status == JobTicketStatuses.RescheduleRequest)
            {
                return BackWithError("This job order has a pending reschedule request awaiting manager approval and can't be updated until it's resolved.");
            }

            if (!ticket.IsAccessibleToFieldTechnician)
            {
                return BackWithError($"This job order isn't accessible yet — it's scheduled for {ticket.ServiceDate:M/d/yyyy}.");
            }

            // Validate status
            if (!StatusOptions.Contains(status))
            {
                return BackWithError("Invalid status.");
            }

            // Only count the CURRENT cycle's files -- ones archived under a
            // reschedule history entry don't count as proof for the new date.
            bool hasUploadedFile = ticket.Submissions != null &&
                       ticket.Submissions.Any(s =>
                           s.RescheduleHistoryID == null &&
                           s.SubmissionHistoryID == null);

            // ============================================================
            // RULE 1:
            // Completed requires at least one uploaded file for proof.
            // ============================================================
            if (status == JobTicketStatuses.Completed && !hasUploadedFile)
            {
                return BackWithError("You must upload at least one photo or file before changing the job order to Completed.");
            }

            // ============================================================
            // RULE 2:
            // In Progress requires remarks and at least one uploaded file.
            //
            // NOTE:
            // If you want the technician to be able to select
            // In Progress first and then upload the file, REMOVE this
            // validation for In Progress.
            // ============================================================
            if (status == JobTicketStatuses.InProgress && string.IsNullOrWhiteSpace(remarks))
            {
                return BackWithError("Please provide a reason in the remarks before marking this job order Inprogress.");
            }

            if (status == JobTicketStatuses.InProgress && !hasUploadedFile)
            {
                return BackWithError("You must upload at least one photo or file for proof in your progress.");
            }
            // ============================================================
            // RULE 3:
            // Cancelled requires remarks
            // ============================================================
            if (status == JobTicketStatuses.Cancelled && string.IsNullOrWhiteSpace(remarks))
            {
                return BackWithError("Please provide a reason in the remarks before cancelling this job order.");
            }

            // ============================================================
            // RULE 4:
            // A Reschedule Request requires remarks explaining why, and -- same
            // as a manager-triggered reschedule -- archives whatever
            // remarks/photos this cycle already had, so the technician starts
            // fresh once the manager acts on the request.
            // ============================================================
            bool isNewReschedule = status == JobTicketStatuses.RescheduleRequest
                && ticket.Status != JobTicketStatuses.RescheduleRequest;

            if (isNewReschedule && string.IsNullOrWhiteSpace(remarks))
            {
                return BackWithError("Please provide a reason in the remarks before requesting a reschedule for this job order.");
            }

            if (isNewReschedule && !hasUploadedFile)
            {
                return BackWithError("You must upload at least one photo or file for proof before requesting a reschedule for this job order.");
            }
            // ============================================================
            // SAVE STATUS
            // ============================================================
            var history = new JobTicketSubmissionHistory
            {
                JobTicketID = ticket.JobTicketID,
                Status = status,
                Remarks = string.IsNullOrWhiteSpace(remarks)
                    ? null
                    : remarks.Trim(),
                DateChanged = DateTime.Now,
                ActorEmployeeID = employeeId.Value
            };

            var currentSubmissions = ticket.Submissions
                .Where(s => s.RescheduleHistoryID == null && s.SubmissionHistoryID == null)
                .ToList();

            foreach (var sub in currentSubmissions)
            {
                history.ArchivedSubmissions.Add(sub);
                sub.SubmissionHistoryID = null;
            }

            _context.JobTicketSubmissionHistories.Add(history);
            await _context.SaveChangesAsync();

            foreach (var sub in currentSubmissions)
            {
                sub.SubmissionHistoryID = history.JobTicketSubmissionHistoryID;
            }

            if (isNewReschedule)
            {
                var rescheduleHistory = new JobTicketRescheduleHistory
                {
                    JobTicketID = ticket.JobTicketID,
                    OldServiceDate = ticket.ServiceDate,
                    NewServiceDate = ticket.ServiceDate,
                    Reason = remarks!.Trim(),
                    PreviousStatus = ticket.Status,
                    PreviousRemarks = ticket.Remarks,
                    DateChanged = DateTime.Now
                };

                var currentSubmissionsBeforeReschedule = ticket.Submissions
                    .Where(s => s.RescheduleHistoryID == null && s.SubmissionHistoryID == null)
                    .ToList();

                foreach (var sub in currentSubmissionsBeforeReschedule)
                {
                    rescheduleHistory.ArchivedSubmissions.Add(sub);
                    sub.RescheduleHistoryID = null;
                }

                _context.JobTicketRescheduleHistories.Add(rescheduleHistory);
                await _context.SaveChangesAsync();

                foreach (var sub in currentSubmissionsBeforeReschedule)
                {
                    sub.RescheduleHistoryID = rescheduleHistory.JobTicketRescheduleHistoryID;
                }

                ticket.Status = JobTicketStatuses.RescheduleRequest;
                ticket.Remarks = null;
            }
            else
            {
                ticket.Status = status;

                if (status == JobTicketStatuses.Completed)
                {
                    ticket.DateCompleted = DateTime.Now;
                }

                ticket.Remarks = string.IsNullOrWhiteSpace(remarks)
                 ? null
               : remarks.Trim();
            }

            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }

        public async Task<IActionResult> OnGetDownloadAsync(int submissionId)
        {
            var submission = await _context.JobTicketSubmissions.FindAsync(submissionId);
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
        public async Task<IActionResult> OnPostCancelEditAsync(int jobTicketId)
        {
            var employeeId = HttpContext.Session.GetInt32("CurrentFieldTechnicianId");

            if (employeeId == null)
            {
                return RedirectToPage("./Select");
            }

            var ticket = await _context.JobTickets
                .Include(t => t.Assignments)
                .FirstOrDefaultAsync(t => t.JobTicketID == jobTicketId);

            if (ticket == null)
            {
                return NotFound();
            }

            var assignment = ticket.Assignments
                .FirstOrDefault(a => a.EmployeeID == employeeId.Value);

            if (assignment == null || !assignment.IsLeader)
            {
                ErrorMessage = "Only the team leader can cancel this edit.";
                return RedirectToPage(new { id = jobTicketId });
            }

            var submissions = await _context.JobTicketSubmissions
                .Where(s =>
                    s.JobTicketID == jobTicketId &&
                    s.RescheduleHistoryID == null &&
                    s.SubmissionHistoryID == null)
                .ToListAsync();

            foreach (var submission in submissions)
            {
                var fullPath = Path.Combine(
                    _env.WebRootPath,
                    submission.FilePath.Replace(
                        "/",
                        Path.DirectorySeparatorChar.ToString()));

                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }

                _context.JobTicketSubmissions.Remove(submission);
            }

            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
