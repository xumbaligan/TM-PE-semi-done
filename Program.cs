using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TM_PE.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Session backs the logged-in identity set at /Login (Employee ID + Role),
// which the RBAC middleware below reads, and also backs the Field
// Technician / Office Staff "who am I" keys those areas already relied on.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// One-time bootstrap: create a default Admin login if no Admin account
// exists yet. Without this there's a chicken-and-egg problem � Admin >
// User Management is itself Admin-only, so nobody could create the first
// Admin account through the UI. Safe to leave in: it only ever acts when
// zero Admin accounts exist, so it's a no-op on every later run.
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        bool hasAdminAccount = db.UserAccounts
            .Any(u => u.Employee != null && u.Employee.RoleType == TM_PE.Model.RoleType.Admin);

        if (!hasAdminAccount)
        {
            var department = db.Departments.OrderBy(d => d.DepartmentId).FirstOrDefault();
            if (department == null)
            {
                department = new TM_PE.Model.Department
                {
                    DepartmentName = "Administration",
                    Description = "Default department created for the initial Admin account.",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                db.Departments.Add(department);
                db.SaveChanges();
            }

            var adminEmployee = new TM_PE.Model.Employee
            {
                FullName = "System Administrator",
                Email = "admin@pakonek.local",
                RoleType = TM_PE.Model.RoleType.Admin,
                DepartmentId = department.DepartmentId,
                IsActive = true,
                HiredAt = DateTime.Now
            };
            db.Employees.Add(adminEmployee);
            db.SaveChanges();

            const string defaultPassword = "20260820";
            var username = TM_PE.Model.UsernamePolicy.BuildUsername(adminEmployee);
            var (hash, salt) = TM_PE.Model.PasswordHasher.Hash(defaultPassword);

            db.UserAccounts.Add(new TM_PE.Model.UserAccount
            {
                EmployeeID = adminEmployee.EmployeeId,
                Username = username,
                PasswordHash = hash,
                PasswordSalt = salt,
                DateCreated = DateTime.Now,
                IsActive = true
            });
            db.SaveChanges();

            Console.WriteLine("=================================================");
            Console.WriteLine(" Default Admin account created.");
            Console.WriteLine($"   Username: {username}");
            Console.WriteLine($"   Password: {defaultPassword}");
            Console.WriteLine("   Log in and change this password immediately.");
            Console.WriteLine("=================================================");
        }
    }
    catch (Exception ex)
    {
        // Most likely cause: migrations haven't been applied yet
        // (dotnet ef database update). Don't crash startup over it.
        Console.WriteLine($"[Admin seed] Skipped: {ex.Message}");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

// Lightweight role-based access control: one place that gates every area by
// the role stored in session at login (see Pages/Login.cshtml.cs), instead of
// repeating an authorization check in every single page. Admin manages
// accounts/roles in Admin > User Management; this middleware is what actually
// enforces who can reach what.
app.Use(async (context, next) =>
{
    var path = context.Request.Path;

    bool IsPublicPath() =>
        path.StartsWithSegments("/Login") ||
        path.StartsWithSegments("/Error") ||
        path.StartsWithSegments("/css") ||
        path.StartsWithSegments("/js") ||
        path.StartsWithSegments("/lib") ||
        path.Value == "/favicon.ico";

    if (IsPublicPath())
    {
        await next();
        return;
    }

    // /Logout only needs *a* session to clear, not a specific role.
    if (path.StartsWithSegments("/Logout"))
    {
        await next();
        return;
    }

    var role = context.Session.GetString("AuthRoleType");

    bool Allowed(params string[] roles) => role != null && roles.Contains(role);

    bool authorized =
        (path.StartsWithSegments("/Admin") && Allowed("Admin")) ||
        // Admin's reach into /Manager is deliberately narrow: only the areas
        // the Dashboard links into (Performance Report, Job Tickets, Office
        // Task, Performance Evaluation, Workload Monitoring) - not the full
        // Manager area (employees, departments, criteria, etc., which Admin
        // already manages through its own /Admin pages instead).
        (path.StartsWithSegments("/Manager/PerformanceReport") && Allowed("Manager", "Admin")) ||
        (path.StartsWithSegments("/Manager/JobTickets") && Allowed("Manager", "Admin")) ||
        (path.StartsWithSegments("/Manager/OfficeTask") && Allowed("Manager", "Admin")) ||
        (path.StartsWithSegments("/Manager/PerformanceEvaluation") && Allowed("Manager", "Admin")) ||
        (path.StartsWithSegments("/Manager/WorkLoadMonitoring") && Allowed("Manager", "Admin")) ||
        (path.StartsWithSegments("/Manager") && Allowed("Manager")) ||
        (path.StartsWithSegments("/FieldTechnician") && Allowed("FieldTechnician")) ||
        (path.StartsWithSegments("/OfficeStaff") && Allowed("OfficeStaff")) ||
        // Shared self-service "My Account" (view info / change password) -
        // any logged-in Manager, Field Technician, or Office Staff. Admin
        // isn't offered this page; it manages its own account like everyone
        // else's, through Admin > User Management.
        (path.StartsWithSegments("/Account") && Allowed("Manager", "FieldTechnician", "OfficeStaff")) ||
        ((path == "/" || path.StartsWithSegments("/Index")) && Allowed("Manager", "Admin"));

    if (!authorized)
    {
        context.Response.Redirect("/Login");
        return;
    }

    await next();
});

app.UseAuthorization();

app.MapRazorPages();

app.Run();