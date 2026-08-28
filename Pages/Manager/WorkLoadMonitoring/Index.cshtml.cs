using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.Manager.WorkLoadMonitoring
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        public IndexModel(AppDbContext db) => _db = db;

        // ---- View 1: Active Tickets per Technician ----
        public List<TechnicianTicketLoad> TechnicianTickets { get; set; } = new();

        // ---- View 2: Active Tasks per Office Staff ----
        public List<StaffTaskLoad> StaffTasks { get; set; } = new();

        // ---- View 3: Employee Workload Summary (moved from Dashboard) ----
        public List<WorkloadItem> Workload { get; set; } = new();

        // ---- View 3b: Field Technician Workload Summary ----
        public List<TechnicianWorkloadItem> TechnicianWorkload { get; set; } = new();

        public async Task OnGetAsync()
        {
            var officeTasks = await _db.OfficeTasks
                .Include(t => t.Assignments).ThenInclude(a => a.Employee)
                .Include(t => t.Activities).ThenInclude(a => a.AssignedEmployee)
                .ToListAsync();

            // Mirrors the overdue check used on the Office Task Index page so this
            // page reflects live status even if no one has opened Office Tasks yet today.
            await RefreshOverdueStatusesAsync(officeTasks);

            var jobTickets = await _db.JobTickets
                .Include(t => t.Assignments).ThenInclude(a => a.Employee)
                .ToListAsync();

            var technicians = await _db.Employees
                .Where(e => e.IsActive && e.RoleType == RoleType.FieldTechnician)
                .Include(e => e.Department)
                .OrderBy(e => e.FullName)
                .ToListAsync();

            var officeStaff = await _db.Employees
                .Where(e => e.IsActive && e.RoleType == RoleType.OfficeStaff)
                .Include(e => e.Department)
                .OrderBy(e => e.FullName)
                .ToListAsync();

            BuildTechnicianTickets(jobTickets, technicians);
            BuildStaffTasks(officeTasks, officeStaff);
            BuildWorkloadSummary(officeTasks, officeStaff);
            BuildTechnicianWorkloadSummary(jobTickets, technicians);
        }

        private async Task RefreshOverdueStatusesAsync(List<Model.OfficeTask> tasks)
        {
            var today = DateTime.Now.Date;
            bool changed = false;

            foreach (var task in tasks)
            {
                if (task.Status != "Completed" && task.DueDate.Date < today)
                {
                    if (task.Status != "Overdue")
                    {
                        task.Status = "Overdue";
                        changed = true;
                    }
                }
                else if (task.Status == "Overdue" && task.DueDate.Date >= today)
                {
                    task.Status = "Pending";
                    changed = true;
                }
            }

            if (changed)
            {
                await _db.SaveChangesAsync();
            }
        }

        private void BuildTechnicianTickets(List<JobTicket> tickets, List<Employee> technicians)
        {
            TechnicianTickets = technicians.Select(tech =>
            {
                var assigned = tickets.Where(t => t.Assignments.Any(a => a.EmployeeID == tech.EmployeeId)).ToList();
                var active = assigned
                    .Where(t => t.Status is JobTicketStatuses.Pending or JobTicketStatuses.InProgress)
                    .OrderBy(t => t.ServiceDate)
                    .ToList();

                return new TechnicianTicketLoad
                {
                    EmployeeId = tech.EmployeeId,
                    FullName = tech.FullName,
                    DepartmentName = tech.Department?.DepartmentName ?? "?",
                    ActiveTicketCount = active.Count,
                    Tickets = active.Select(t => new TicketSummary
                    {
                        TicketNumber = t.TicketNumber,
                        JobType = t.JobType,
                        ClientFullName = t.ClientFullName,
                        Status = t.Status,
                        ServiceDate = t.ServiceDate,
                        DueDate = t.DateOfCompletion
                    }).ToList()
                };
            })
            .OrderByDescending(t => t.ActiveTicketCount)
            .ToList();
        }

        private void BuildStaffTasks(List<Model.OfficeTask> tasks, List<Employee> officeStaff)
        {
            StaffTasks = officeStaff.Select(emp =>
            {
                var assigned = tasks.Where(t => t.Assignments.Any(a => a.EmployeeID == emp.EmployeeId)).ToList();
                var active = assigned
                    .Where(t => t.Status is "Pending" or "In Progress" or "Overdue")
                    .OrderBy(t => t.DueDate)
                    .ToList();

                return new StaffTaskLoad
                {
                    EmployeeId = emp.EmployeeId,
                    FullName = emp.FullName,
                    DepartmentName = emp.Department?.DepartmentName ?? "?",
                    ActiveTaskCount = active.Count,
                    Tasks = active.Select(t => new TaskSummary
                    {
                        TaskNumber = t.TaskNumber,
                        TaskName = t.TaskName,
                        Status = t.Status,
                        DateCreated = t.DateCreated,
                        DueDate = t.DueDate
                    }).ToList()
                };
            })
            .OrderByDescending(s => s.ActiveTaskCount)
            .ToList();
        }

        // Builds a per-Office-Staff workload snapshot from the same signals the
        // Office Task module already tracks: task assignments, per-activity
        // assignment, task status/overdue, and the computed task Score.
        private void BuildWorkloadSummary(List<Model.OfficeTask> tasks, List<Employee> officeStaff)
        {
            Workload = officeStaff.Select(emp =>
            {
                var assignedTasks = tasks.Where(t => t.Assignments.Any(a => a.EmployeeID == emp.EmployeeId)).ToList();
                var activeTasks = assignedTasks.Count(t => t.Status is "Pending" or "In Progress" or "Overdue");
                var overdueTasks = assignedTasks.Count(t => t.Status == "Overdue");
                var completedTasks = assignedTasks.Count(t => t.Status == "Completed");

                var assignedActivities = tasks
                    .SelectMany(t => t.Activities)
                    .Where(a => a.AssignedEmployeeID == emp.EmployeeId)
                    .ToList();
                var pendingActivities = assignedActivities.Count(a => a.Status != "Approved");

                var avgScore = assignedTasks.Any() ? Math.Round(assignedTasks.Average(t => t.Score), 1) : 0;

                // Simple, transparent weighting: an active task counts more than a
                // pending activity since it carries more responsibility.
                var points = (activeTasks * 2) + pendingActivities + (overdueTasks * 2);
                var level = points switch
                {
                    <= 2 => "Light",
                    <= 6 => "Moderate",
                    _ => "Heavy"
                };

                return new WorkloadItem
                {
                    EmployeeId = emp.EmployeeId,
                    FullName = emp.FullName,
                    DepartmentName = emp.Department?.DepartmentName ?? "?",
                    ActiveTasks = activeTasks,
                    OverdueTasks = overdueTasks,
                    CompletedTasks = completedTasks,
                    TotalTasks = assignedTasks.Count,
                    PendingActivities = pendingActivities,
                    AvgScore = avgScore,
                    WorkloadPoints = points,
                    WorkloadLevel = level
                };
            })
            .OrderByDescending(w => w.WorkloadPoints)
            .ToList();
        }

        // Builds a per-Field-Technician workload snapshot from job ticket
        // assignments, status, and service date (used as the "due" signal
        // since JobTicket has no separate due date field).
        private void BuildTechnicianWorkloadSummary(List<JobTicket> tickets, List<Employee> technicians)
        {
            var today = DateTime.Now.Date;

            TechnicianWorkload = technicians.Select(tech =>
            {
                var assignedTickets = tickets.Where(t => t.Assignments.Any(a => a.EmployeeID == tech.EmployeeId)).ToList();
                var activeTickets = assignedTickets.Count(t => t.Status is JobTicketStatuses.Pending or JobTicketStatuses.InProgress);
                var overdueTickets = assignedTickets.Count(t =>
                    t.Status is JobTicketStatuses.Pending or JobTicketStatuses.InProgress
                    && t.ServiceDate.Date < today);
                var completedTickets = assignedTickets.Count(t => t.Status == JobTicketStatuses.Completed);

                // Simple, transparent weighting consistent with the Office Staff
                // summary: an active ticket counts more, an overdue one counts extra.
                var points = (activeTickets * 2) + (overdueTickets * 2);
                var level = points switch
                {
                    <= 2 => "Light",
                    <= 6 => "Moderate",
                    _ => "Heavy"
                };

                return new TechnicianWorkloadItem
                {
                    EmployeeId = tech.EmployeeId,
                    FullName = tech.FullName,
                    DepartmentName = tech.Department?.DepartmentName ?? "?",
                    ActiveTickets = activeTickets,
                    OverdueTickets = overdueTickets,
                    CompletedTickets = completedTickets,
                    TotalTickets = assignedTickets.Count,
                    WorkloadPoints = points,
                    WorkloadLevel = level
                };
            })
            .OrderByDescending(w => w.WorkloadPoints)
            .ToList();
        }

        public class TechnicianTicketLoad
        {
            public int EmployeeId { get; set; }
            public string FullName { get; set; } = string.Empty;
            public string DepartmentName { get; set; } = string.Empty;
            public int ActiveTicketCount { get; set; }
            public List<TicketSummary> Tickets { get; set; } = new();
        }

        public class TicketSummary
        {
            public string TicketNumber { get; set; } = string.Empty;
            public string JobType { get; set; } = string.Empty;
            public string ClientFullName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public DateTime ServiceDate { get; set; }
            public DateTime? DueDate { get; set; }
        }

        public class StaffTaskLoad
        {
            public int EmployeeId { get; set; }
            public string FullName { get; set; } = string.Empty;
            public string DepartmentName { get; set; } = string.Empty;
            public int ActiveTaskCount { get; set; }
            public List<TaskSummary> Tasks { get; set; } = new();
        }

        public class TaskSummary
        {
            public string TaskNumber { get; set; } = string.Empty;
            public string TaskName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public DateTime DateCreated { get; set; }
            public DateTime DueDate { get; set; }
        }

        public class WorkloadItem
        {
            public int EmployeeId { get; set; }
            public string FullName { get; set; } = string.Empty;
            public string DepartmentName { get; set; } = string.Empty;
            public int ActiveTasks { get; set; }
            public int OverdueTasks { get; set; }
            public int CompletedTasks { get; set; }
            public int TotalTasks { get; set; }
            public int PendingActivities { get; set; }
            public decimal AvgScore { get; set; }
            public int WorkloadPoints { get; set; }
            public string WorkloadLevel { get; set; } = "Light";
        }

        public class TechnicianWorkloadItem
        {
            public int EmployeeId { get; set; }
            public string FullName { get; set; } = string.Empty;
            public string DepartmentName { get; set; } = string.Empty;
            public int ActiveTickets { get; set; }
            public int OverdueTickets { get; set; }
            public int CompletedTickets { get; set; }
            public int TotalTickets { get; set; }
            public int WorkloadPoints { get; set; }
            public string WorkloadLevel { get; set; } = "Light";
        }
    }
}