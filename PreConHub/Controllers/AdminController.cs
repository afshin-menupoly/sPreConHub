using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PreConHub.Data;
using PreConHub.Models.Entities;
using PreConHub.Models.ViewModels;
using System.Security.Claims;

namespace PreConHub.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<AdminController> logger)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        // GET: /Admin/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var viewModel = new AdminDashboardViewModel
            {
                TotalBuilders = await _userManager.GetUsersInRoleAsync("Builder").ContinueWith(t => t.Result.Count),
                TotalPurchasers = await _userManager.GetUsersInRoleAsync("Purchaser").ContinueWith(t => t.Result.Count),
                TotalLawyers = await _userManager.GetUsersInRoleAsync("Lawyer").ContinueWith(t => t.Result.Count),
                TotalAdmins = await _userManager.GetUsersInRoleAsync("Admin").ContinueWith(t => t.Result.Count),
                
                TotalProjects = await _context.Projects.CountAsync(),
                TotalUnits = await _context.Units.CountAsync(),
                
                RecentUsers = await _context.Users
                    .OrderByDescending(u => u.CreatedAt)
                    .Take(10)
                    .Select(u => new UserListItemViewModel
                    {
                        UserId = u.Id,
                        FullName = $"{u.FirstName} {u.LastName}",
                        Email = u.Email ?? "",
                        UserType = u.UserType,
                        IsActive = u.IsActive,
                        LastLoginAt = u.LastLoginAt,
                        CreatedAt = u.CreatedAt
                    }).ToListAsync(),
                    
                RecentlyActiveUsers = await _context.Users
                    .Where(u => u.LastLoginAt != null)
                    .OrderByDescending(u => u.LastLoginAt)
                    .Take(10)
                    .Select(u => new UserListItemViewModel
                    {
                        UserId = u.Id,
                        FullName = $"{u.FirstName} {u.LastName}",
                        Email = u.Email ?? "",
                        UserType = u.UserType,
                        IsActive = u.IsActive,
                        LastLoginAt = u.LastLoginAt,
                        CreatedAt = u.CreatedAt
                    }).ToListAsync()
            };

            return View(viewModel);
        }

        // GET: /Admin/Users
        public async Task<IActionResult> Users(string? search, string? userType, string? status, int page = 1)
        {
            var query = _context.Users.AsQueryable();

            // Search filter (without Company since it doesn't exist)
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(u => 
                    (u.Email != null && u.Email.ToLower().Contains(search)) ||
                    (u.FirstName != null && u.FirstName.ToLower().Contains(search)) ||
                    (u.LastName != null && u.LastName.ToLower().Contains(search)));
            }

            // User type filter
            if (!string.IsNullOrWhiteSpace(userType) && Enum.TryParse<UserType>(userType, out var type))
            {
                query = query.Where(u => u.UserType == type);
            }

            // Status filter
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status == "active")
                    query = query.Where(u => u.IsActive);
                else if (status == "inactive")
                    query = query.Where(u => !u.IsActive);
            }

            var totalCount = await query.CountAsync();
            var pageSize = 20;
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserListItemViewModel
                {
                    UserId = u.Id,
                    FullName = $"{u.FirstName} {u.LastName}",
                    Email = u.Email ?? "",
                    Phone = u.PhoneNumber,
                    UserType = u.UserType,
                    IsActive = u.IsActive,
                    EmailConfirmed = u.EmailConfirmed,
                    LastLoginAt = u.LastLoginAt,
                    CreatedAt = u.CreatedAt
                }).ToListAsync();

            var viewModel = new UserListViewModel
            {
                Users = users,
                Search = search,
                UserTypeFilter = userType,
                StatusFilter = status,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalCount = totalCount
            };

            return View(viewModel);
        }

        // GET: /Admin/UserDetail/userId
        public async Task<IActionResult> UserDetail(string id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            var viewModel = new AdminUserDetailViewModel
            {
                UserId = user.Id,
                Email = user.Email ?? "",
                FirstName = user.FirstName,
                LastName = user.LastName,
                Phone = user.PhoneNumber,
                UserType = user.UserType,
                Roles = roles.ToList(),
                IsActive = user.IsActive,
                EmailConfirmed = user.EmailConfirmed,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            };

            // Load type-specific data
            switch (user.UserType)
            {
                case UserType.Builder:
                    await LoadBuilderData(viewModel, user.Id);
                    break;
                case UserType.Purchaser:
                    await LoadPurchaserData(viewModel, user.Id);
                    break;
                case UserType.Lawyer:
                    await LoadLawyerData(viewModel, user.Id);
                    break;
            }

            // Load recent activity
            viewModel.RecentActivity = await GetUserActivity(user.Id, user.UserType);

            return View(viewModel);
        }

        private async Task LoadBuilderData(AdminUserDetailViewModel viewModel, string userId)
        {
            viewModel.BuilderProjects = await _context.Projects
                .Where(p => p.BuilderId == userId)
                .Select(p => new BuilderProjectSummary
                {
                    ProjectId = p.Id,
                    ProjectName = p.Name,
                    Address = $"{p.Address}, {p.City}",
                    TotalUnits = p.Units.Count,
                    PendingUnits = p.Units.Count(u => u.Status == UnitStatus.Pending),
                    AtRiskUnits = p.Units.Count(u => u.Status == UnitStatus.AtRisk),
                    ClosedUnits = p.Units.Count(u => u.Status == UnitStatus.Closed),
                    CreatedAt = p.CreatedAt
                }).ToListAsync();

            viewModel.TotalProjects = viewModel.BuilderProjects.Count;
            viewModel.TotalUnits = viewModel.BuilderProjects.Sum(p => p.TotalUnits);
        }

        private async Task LoadPurchaserData(AdminUserDetailViewModel viewModel, string userId)
        {
            viewModel.PurchaserUnits = await _context.UnitPurchasers
                .Where(up => up.PurchaserId == userId)
                .Include(up => up.Unit)
                    .ThenInclude(u => u.Project)
                .Include(up => up.MortgageInfo)
                .Select(up => new PurchaserUnitSummary
                {
                    UnitId = up.UnitId,
                    UnitNumber = up.Unit.UnitNumber,
                    ProjectName = up.Unit.Project.Name,
                    PurchasePrice = up.Unit.PurchasePrice,
                    ClosingDate = up.Unit.ClosingDate,
                    Status = up.Unit.Status,
                    IsPrimary = up.IsPrimaryPurchaser,
                    HasMortgageApproval = up.MortgageInfo != null && up.MortgageInfo.HasMortgageApproval,
                    MortgageAmount = (up.MortgageInfo != null && up.MortgageInfo.ApprovedAmount.HasValue) ? up.MortgageInfo.ApprovedAmount.Value : 0m
                }).ToListAsync();
        }

        private async Task LoadLawyerData(AdminUserDetailViewModel viewModel, string userId)
        {
            viewModel.LawyerAssignments = await _context.LawyerAssignments
                .Where(la => la.LawyerId == userId && la.IsActive)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Project)
                .Select(la => new LawyerAssignmentSummary
                {
                    AssignmentId = la.Id,
                    UnitId = la.UnitId ?? 0,
                    UnitNumber = la.Unit != null ? la.Unit.UnitNumber : "N/A",
                    ProjectName = la.Unit != null ? la.Unit.Project.Name : la.Project.Name,
                    Role = la.Role,
                    ReviewStatus = la.ReviewStatus,
                    AssignedAt = la.AssignedAt
                }).ToListAsync();
        }

        private async Task<List<UserActivityItem>> GetUserActivity(string userId, UserType userType)
        {
            var activities = new List<UserActivityItem>();

            switch (userType)
            {
                case UserType.Builder:
                    var recentProjects = await _context.Projects
                        .Where(p => p.BuilderId == userId)
                        .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
                        .Take(5)
                        .Select(p => new { p.Name, p.CreatedAt, p.UpdatedAt })
                        .ToListAsync();

                    foreach (var p in recentProjects)
                    {
                        activities.Add(new UserActivityItem
                        {
                            Action = p.UpdatedAt.HasValue ? $"Updated project: {p.Name}" : $"Created project: {p.Name}",
                            Timestamp = p.UpdatedAt ?? p.CreatedAt,
                            Icon = "bi-building"
                        });
                    }

                    var recentUnits = await _context.Units
                        .Include(u => u.Project)
                        .Where(u => u.Project.BuilderId == userId)
                        .OrderByDescending(u => u.UpdatedAt ?? u.CreatedAt)
                        .Take(5)
                        .Select(u => new { u.UnitNumber, u.Project.Name, u.CreatedAt, u.UpdatedAt })
                        .ToListAsync();

                    foreach (var u in recentUnits)
                    {
                        activities.Add(new UserActivityItem
                        {
                            Action = $"Unit {u.UnitNumber} in {u.Name}",
                            Timestamp = u.UpdatedAt ?? u.CreatedAt,
                            Icon = "bi-house"
                        });
                    }
                    break;

                case UserType.Purchaser:
                    var purchaserUnits = await _context.UnitPurchasers
                        .Where(up => up.PurchaserId == userId)
                        .Include(up => up.Unit)
                        .Include(up => up.MortgageInfo)
                        .ToListAsync();

                    foreach (var up in purchaserUnits)
                    {
                        if (up.MortgageInfo?.UpdatedAt != null)
                        {
                            activities.Add(new UserActivityItem
                            {
                                Action = $"Updated mortgage info for Unit {up.Unit.UnitNumber}",
                                Timestamp = up.MortgageInfo.UpdatedAt.Value,
                                Icon = "bi-bank"
                            });
                        }
                    }
                    break;

                case UserType.Lawyer:
                    var lawyerNotes = await _context.Set<LawyerNote>()
                        .Include(n => n.LawyerAssignment)
                            .ThenInclude(la => la.Unit)
                        .Where(n => n.LawyerAssignment.LawyerId == userId)
                        .OrderByDescending(n => n.CreatedAt)
                        .Take(10)
                        .ToListAsync();

                    foreach (var note in lawyerNotes)
                    {
                        activities.Add(new UserActivityItem
                        {
                            Action = $"Added {note.NoteType} note for Unit {note.LawyerAssignment.Unit?.UnitNumber ?? "N/A"}",
                            Timestamp = note.CreatedAt,
                            Icon = "bi-chat-left-text"
                        });
                    }
                    break;
            }

            return activities.OrderByDescending(a => a.Timestamp).Take(15).ToList();
        }

        // POST: /Admin/ToggleUserStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);

            _logger.LogInformation("Admin {AdminId} toggled status for user {UserId} to {Status}",
                _userManager.GetUserId(User), userId, user.IsActive ? "Active" : "Inactive");

            TempData["Success"] = $"User {user.Email} is now {(user.IsActive ? "Active" : "Inactive")}.";
            return RedirectToAction(nameof(UserDetail), new { id = userId });
        }

        // POST: /Admin/ImpersonateUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImpersonateUser(string userId)
        {
            var currentUserId = _userManager.GetUserId(User);
            var targetUser = await _userManager.FindByIdAsync(userId);

            if (targetUser == null)
                return NotFound();

            // Don't allow impersonating other admins
            if (await _userManager.IsInRoleAsync(targetUser, "Admin") || targetUser.UserType == UserType.PlatformAdmin)
            {
                TempData["Error"] = "Cannot impersonate other administrators.";
                return RedirectToAction(nameof(UserDetail), new { id = userId });
            }

            _logger.LogWarning("Admin {AdminId} started impersonating user {UserId} ({Email})",
                currentUserId, userId, targetUser.Email);

            // Store original admin ID in session
            HttpContext.Session.SetString("OriginalAdminId", currentUserId ?? "");
            HttpContext.Session.SetString("ImpersonatingUserId", userId);
            HttpContext.Session.SetString("ImpersonatingUserEmail", targetUser.Email ?? "");

            // Sign in as the target user
            await _signInManager.SignOutAsync();
            await _signInManager.SignInAsync(targetUser, isPersistent: false);

            TempData["Warning"] = $"You are now viewing as {targetUser.Email}. Click 'Stop Impersonation' in the header to return.";

            // Redirect based on user type
            return targetUser.UserType switch
            {
                UserType.Builder => RedirectToAction("Index", "Projects"),
                UserType.Purchaser => RedirectToAction("Dashboard", "Purchaser"),
                UserType.Lawyer => RedirectToAction("Dashboard", "Lawyer"),
                UserType.PlatformAdmin => RedirectToAction("Dashboard", "Admin"),
                _ => RedirectToAction("Index", "Home")
            };
        }

        // POST: /Admin/StopImpersonation
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StopImpersonation()
        {
            var originalAdminId = HttpContext.Session.GetString("OriginalAdminId");
            var impersonatedUserId = HttpContext.Session.GetString("ImpersonatingUserId");

            if (string.IsNullOrEmpty(originalAdminId))
            {
                return RedirectToAction("Index", "Home");
            }

            var adminUser = await _userManager.FindByIdAsync(originalAdminId);
            if (adminUser == null)
            {
                await _signInManager.SignOutAsync();
                return RedirectToAction("Login", "Account");
            }

            _logger.LogInformation("Admin {AdminId} stopped impersonating user {UserId}",
                originalAdminId, impersonatedUserId);

            // Clear impersonation session
            HttpContext.Session.Remove("OriginalAdminId");
            HttpContext.Session.Remove("ImpersonatingUserId");
            HttpContext.Session.Remove("ImpersonatingUserEmail");

            // Sign back in as admin
            await _signInManager.SignOutAsync();
            await _signInManager.SignInAsync(adminUser, isPersistent: false);

            TempData["Success"] = "Impersonation ended. You are now logged in as yourself.";
            return RedirectToAction(nameof(Dashboard));
        }

        // GET: /Admin/ResetPassword/userId
        public async Task<IActionResult> ResetPassword(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            var viewModel = new AdminResetPasswordViewModel
            {
                UserId = id,
                Email = user.Email ?? "",
                FullName = $"{user.FirstName} {user.LastName}"
            };

            return View(viewModel);
        }

        // POST: /Admin/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(AdminResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
                return NotFound();

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (result.Succeeded)
            {
                _logger.LogWarning("Admin {AdminId} reset password for user {UserId} ({Email})",
                    _userManager.GetUserId(User), model.UserId, user.Email);

                TempData["Success"] = $"Password reset successfully for {user.Email}.";
                return RedirectToAction(nameof(UserDetail), new { id = model.UserId });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        // POST: /Admin/ResendInvitation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendInvitation(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            TempData["Info"] = $"Resend invitation feature coming soon for {user.Email}.";
            return RedirectToAction(nameof(UserDetail), new { id = userId });
        }
    }
}
