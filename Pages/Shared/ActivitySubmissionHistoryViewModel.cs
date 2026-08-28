using TM_PE.Model;

namespace TM_PE.Pages.Shared
{
    // Shared by OfficeStaff/Details and Manager/OfficeTask/Details so both
    // render one activity's full "History of Submission" - every attempt ever
    // uploaded for it, oldest overwriting nothing - in exactly the same shape.
    // See _ActivitySubmissionHistoryModal.cshtml.
    public class ActivitySubmissionHistoryViewModel
    {
        public TaskActivity Activity { get; set; } = default!;

        // Newest first.
        public List<ActivitySubmission> Attempts { get; set; } = new();
    }
}
