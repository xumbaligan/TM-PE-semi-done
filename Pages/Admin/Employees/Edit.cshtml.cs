using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.Admin.Employees;

public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    public EditModel(AppDbContext db) => _db = db;

    [BindProperty] public Employee Employee { get; set; } = new();
    public List<Department> AllDepartments { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var e = await _db.Employees.FindAsync(id);
        if (e == null) return NotFound();
        Employee = e;
        LoadLists();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var namePattern = new System.Text.RegularExpressions.Regex(@"^[A-Za-z\s'-]+$");
        var emailPattern = new System.Text.RegularExpressions.Regex(@"^[^@\s]+@[^@\s]+\.[a-zA-Z]{2,6}$");

        // Allowed email domains only
        var allowedDomains = new[]
        {
        "gmail.com", "yahoo.com", "outlook.com", "hotmail.com",
        "icloud.com", "live.com", "msn.com", "mail.com"
    };

        var existingEmail = await _db.Employees
            .FirstOrDefaultAsync(e => e.Email == Employee.Email && e.EmployeeId != Employee.EmployeeId);

        if (!ModelState.IsValid)
        {
            LoadLists(); return Page();
        }

        // The department must belong to the same role type as the employee
        var department = await _db.Departments.FindAsync(Employee.DepartmentId);
        if (department == null || department.RoleType != Employee.RoleType)
        {
            ModelState.AddModelError(
                "Employee.DepartmentId",
                "Select a department that matches the chosen role type.");
        }

        if (!namePattern.IsMatch(Employee.FullName))
        {
            ModelState.AddModelError("Employee.FullName",
                "Remove the Symbols, Numbers, or Characters (1!@#$%...)");
        }

        if (!emailPattern.IsMatch(Employee.Email))
        {
            ModelState.AddModelError("Employee.Email",
                "Please enter a valid email address.");
        }
        else
        {
            // Extract domain from email (part after @)
            var domain = Employee.Email.Split('@').Last().ToLower();

            if (!allowedDomains.Contains(domain))
            {
                ModelState.AddModelError("Employee.Email",
                    $"Email domain '@{domain}' is not allowed. Use: gmail.com, yahoo.com, outlook.com, etc.");
            }
            else if (existingEmail != null)
            {
                ModelState.AddModelError("Employee.Email",
                    "This email is already taken.");
            }
        }

        if (!ModelState.IsValid)
        {
            LoadLists(); return Page();
        }

        _db.Attach(Employee).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    private void LoadLists() =>
        AllDepartments = _db.Departments
            .Where(d => d.IsActive || d.DepartmentId == Employee.DepartmentId)
            .OrderBy(d => d.DepartmentName)
            .ToList();
}
