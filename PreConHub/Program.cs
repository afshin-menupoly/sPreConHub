using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PreConHub.Data;
using PreConHub.Models.Entities;
using PreConHub.Services;
using System.Security.Claims;
using PreConHub.Hubs;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// DATABASE CONFIGURATION
// ============================================

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddTransient<IPdfService, PdfService>();

builder.Services.AddHttpClient("ClaudeApi");
builder.Services.AddScoped<IDocumentAnalysisService, DocumentAnalysisService>();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ============================================
// IDENTITY CONFIGURATION
// ============================================

builder.Services.AddDefaultIdentity<ApplicationUser>(options => {
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// ============================================
// TRACK LAST LOGIN - Cookie Events
// ============================================
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    
    // Track LastLoginAt when user signs in
    options.Events.OnSignedIn = async context =>
    {
        var userManager = context.HttpContext.RequestServices
            .GetRequiredService<UserManager<ApplicationUser>>();
        
        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await userManager.UpdateAsync(user);
            }
        }
    };
});

// ============================================
// APPLICATION SERVICES
// ============================================

builder.Services.AddScoped<ISoaCalculationService, SoaCalculationService>();
builder.Services.AddScoped<IReportExportService, ReportExportService>();
builder.Services.AddScoped<IShortfallAnalysisService, ShortfallAnalysisService>();
builder.Services.AddScoped<IProjectSummaryService, ProjectSummaryService>();

builder.Services.AddHttpContextAccessor();

// ============================================
// MVC CONFIGURATION
// ============================================

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// ============================================
// CORS
// ============================================

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailService, EmailService>();

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// Notification Services
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<INotificationHubService, NotificationHubService>();

// Background service for scheduled notification checks
builder.Services.AddHostedService<NotificationBackgroundService>();

// SignalR for real-time notifications
builder.Services.AddSignalR();


// ============================================
// BUILD APPLICATION
// ============================================

var app = builder.Build();

// ============================================
// MIDDLEWARE PIPELINE
// ============================================

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseCors("AllowAll");

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// SignalR Hub mapping
app.MapHub<NotificationHub>("/notificationHub");

// ============================================
// ROUTE CONFIGURATION
// ============================================

app.MapControllerRoute(
    name: "api",
    pattern: "api/{controller}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// ============================================
// DATABASE INITIALIZATION
// ============================================

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        
        context.Database.Migrate();
        
        await SeedRolesAsync(roleManager);
        await SeedAdminUserAsync(userManager);
        await SeedSuperAdminAsync(userManager);
        await SeedBuilderQuotasAsync(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

app.Run();

// ============================================
// SEED METHODS
// ============================================

static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
{
    string[] roles = { "Admin", "Builder", "Purchaser", "Lawyer", "MarketingAgency", "SuperAdmin" };
    
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}

static async Task SeedAdminUserAsync(UserManager<ApplicationUser> userManager)
{
    // CHANGE THIS EMAIL to your actual email!
    var adminEmail = "afshin@preconhub.com";
    
    if (await userManager.FindByEmailAsync(adminEmail) == null)
    {
        var adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FirstName = "Afshin",
            LastName = "Admin",
            UserType = UserType.PlatformAdmin,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        
        // CHANGE THIS PASSWORD after first login!
        var result = await userManager.CreateAsync(adminUser, "Afshin@123!");
        
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
            Console.WriteLine($"✓ Admin user created: {adminEmail} / Password: Afshin@123!");
        }
        else
        {
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"✗ Admin creation error: {error.Description}");
            }
        }
    }
    else
    {
        Console.WriteLine($"Admin user already exists: {adminEmail}");
    }
}

static async Task SeedSuperAdminAsync(UserManager<ApplicationUser> userManager)
{
    var superAdminEmail = "info@afshahin.com";
    var existingUser = await userManager.FindByEmailAsync(superAdminEmail);

    if (existingUser == null)
    {
        // Create the SuperAdmin user
        var superAdmin = new ApplicationUser
        {
            UserName = superAdminEmail,
            Email = superAdminEmail,
            FirstName = "Afshin",
            LastName = "SuperAdmin",
            UserType = UserType.PlatformAdmin,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(superAdmin, "Afshin@123!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(superAdmin, "Admin");
            await userManager.AddToRoleAsync(superAdmin, "SuperAdmin");
            Console.WriteLine($"SuperAdmin user created: {superAdminEmail}");
        }
        else
        {
            foreach (var error in result.Errors)
                Console.WriteLine($"SuperAdmin creation error: {error.Description}");
        }
    }
    else
    {
        // Ensure existing user has SuperAdmin + Admin roles
        if (!await userManager.IsInRoleAsync(existingUser, "SuperAdmin"))
        {
            await userManager.AddToRoleAsync(existingUser, "SuperAdmin");
            Console.WriteLine($"SuperAdmin role assigned to existing user: {superAdminEmail}");
        }
        if (!await userManager.IsInRoleAsync(existingUser, "Admin"))
        {
            await userManager.AddToRoleAsync(existingUser, "Admin");
        }
    }
}

static async Task SeedBuilderQuotasAsync(ApplicationDbContext context)
{
    // Set MaxProjects=4 for existing builders who still have default (1)
    var builders = await context.Users
        .Where(u => u.UserType == UserType.Builder && u.MaxProjects == 1)
        .ToListAsync();

    foreach (var b in builders)
    {
        // Only upgrade if they already have projects (existing active builder)
        var projectCount = await context.Projects.CountAsync(p => p.BuilderId == b.Id);
        if (projectCount > 0)
        {
            b.MaxProjects = 4;
        }
    }

    // Set MaxUnits=10 for existing projects that have null MaxUnits
    var projects = await context.Projects
        .Where(p => p.MaxUnits == null)
        .ToListAsync();

    foreach (var p in projects)
    {
        p.MaxUnits = 10;
    }

    if (builders.Any() || projects.Any())
    {
        await context.SaveChangesAsync();
        Console.WriteLine($"Builder quotas seeded: {builders.Count(b => b.MaxProjects == 4)} builders set to MaxProjects=4, {projects.Count} projects set to MaxUnits=10");
    }
}
