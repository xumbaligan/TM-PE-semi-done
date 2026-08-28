using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TM_PE.Data;
using TM_PE.Model;

namespace TM_PE.Pages.Admin.Employees;

public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    public CreateModel(AppDbContext db) => _db = db;

    [BindProperty] public Employee Employee { get; set; } = new() { IsActive = true };
    public List<Department> AllDepartments { get; set; } = new();

    public void OnGet() => LoadLists();

    public async Task<IActionResult> OnPostAsync()
    {
        var existingEmail = await _db.Employees
       .FirstOrDefaultAsync(e => e.Email == Employee.Email);

        var existingName = await _db.Employees
            .FirstOrDefaultAsync(e => e.FullName == Employee.FullName);

        var namePattern = new System.Text.RegularExpressions.Regex(@"^[A-Za-z\s'-]+$");
        var emailPattern = new System.Text.RegularExpressions.Regex(@"^[^@\s]+@[^@\s]+\.[a-zA-Z]{2,6}$");

        // Allowed email domains
        var allowedDomains = new[]
        {
    "gmail.com", "yahoo.com", "outlook.com", "hotmail.com",
    "icloud.com", "live.com", "msn.com", "mail.com"
};

        if (!ModelState.IsValid)
        {
            LoadLists();
            return Page();
        }

        // Validate Full Name
        if (!namePattern.IsMatch(Employee.FullName))
        {
            ModelState.AddModelError(
                "Employee.FullName",
                "Full name should only contain letters, spaces, apostrophes, or hyphens."
            );
        }

        // Check duplicate Full Name
        if (existingName != null)
        {
            ModelState.AddModelError(
                "Employee.FullName",
                "This full name is already taken."
            );
        }

        // Validate Email Format
        if (!emailPattern.IsMatch(Employee.Email))
        {
            ModelState.AddModelError(
                "Employee.Email",
                "Please enter a valid email address."
            );
        }
        else
        {
            // Extract domain
            var domain = Employee.Email.Split('@').Last().ToLower();

            // Validate allowed domain
            if (!allowedDomains.Contains(domain))
            {
                ModelState.AddModelError(
                    "Employee.Email",
                    $"Email domain '@{domain}' is not allowed."
                );
            }
        }

        // Check duplicate Email
        if (existingEmail != null)
        {
            ModelState.AddModelError(
                "Employee.Email",
                "This email is already taken."
            );
        }

        // The department must belong to the same role type as the employee
        var department = await _db.Departments.FindAsync(Employee.DepartmentId);
        if (department == null || department.RoleType != Employee.RoleType)
        {
            ModelState.AddModelError(
                "Employee.DepartmentId",
                "Select a department that matches the chosen role type."
            );
        }

        // If any validation failed
        if (!ModelState.IsValid)
        {
            LoadLists();
            return Page();
        }

        // Save Employee
        _db.Employees.Add(Employee);
        await _db.SaveChangesAsync();

        return RedirectToPage("Index");
    }

    private void LoadLists() =>
        AllDepartments = _db.Departments
            .Where(d => d.IsActive)
            .OrderBy(d => d.DepartmentName)
            .ToList();
}


