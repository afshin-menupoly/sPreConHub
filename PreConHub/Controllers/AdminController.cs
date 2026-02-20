using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PreConHub.Data;
using PreConHub.Models.Entities;
using PreConHub.Models.ViewModels;
using PreConHub.Services;
using System.Security.Claims;

namespace PreConHub.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailService _emailService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            IEmailService emailService,
            ILogger<AdminController> logger)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _emailService = emailService;
            _logger = logger;
        }

        // ===== SuperAdmin Helpers =====
        private bool IsCurrentUserSuperAdmin()
            => User.IsInRole("SuperAdmin");

        private async Task<bool> IsTargetSuperAdmin(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            return user != null && await _userManager.IsInRoleAsync(user, "SuperAdmin");
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
                    CreatedAt = u.CreatedAt,
                    InvitedByUserId = u.CreatedByUserId,
                    InvitedByName = u.CreatedByUser != null
                        ? u.CreatedByUser.FirstName + " " + u.CreatedByUser.LastName
                        : null
                }).ToListAsync();

            // Populate IsSuperAdmin from roles (can't query via EF)
            var superAdmins = await _userManager.GetUsersInRoleAsync("SuperAdmin");
            var superAdminIds = superAdmins.Select(u => u.Id).ToHashSet();
            foreach (var u in users)
                u.IsSuperAdmin = superAdminIds.Contains(u.UserId);

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
            var user = await _context.Users
                .Include(u => u.CreatedByUser)
                .FirstOrDefaultAsync(u => u.Id == id);
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
                CompanyName = user.CompanyName,
                UserType = user.UserType,
                Roles = roles.ToList(),
                IsActive = user.IsActive,
                EmailConfirmed = user.EmailConfirmed,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                IsSuperAdmin = roles.Contains("SuperAdmin"),
                IsCurrentUserSuperAdmin = IsCurrentUserSuperAdmin(),
                MaxProjects = user.MaxProjects,
                InvitedByUserId = user.CreatedByUserId,
                InvitedByName = user.CreatedByUser != null
                    ? $"{user.CreatedByUser.FirstName} {user.CreatedByUser.LastName}"
                    : null
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
                    MaxUnits = p.MaxUnits,
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

            // SuperAdmin protection
            if (await IsTargetSuperAdmin(userId) && !IsCurrentUserSuperAdmin())
            {
                TempData["Error"] = "Only Super Admins can modify Super Admin accounts.";
                return RedirectToAction(nameof(UserDetail), new { id = userId });
            }

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

            // Don't allow impersonating SuperAdmins or other admins
            if (await _userManager.IsInRoleAsync(targetUser, "SuperAdmin"))
            {
                TempData["Error"] = "Cannot impersonate Super Admin accounts.";
                return RedirectToAction(nameof(UserDetail), new { id = userId });
            }
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

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetLink = Url.Action("ResetPassword", "Account", new { area = "Identity", code = token, email = user.Email }, Request.Scheme);
            await _emailService.SendPasswordResetEmailAsync(user.Email ?? "", $"{user.FirstName} {user.LastName}", resetLink ?? "");

            TempData["Success"] = $"Invitation re-sent to {user.Email}.";
            return RedirectToAction(nameof(UserDetail), new { id = userId });
        }

        // POST: /Admin/SendPasswordResetEmail
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendPasswordResetEmail(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetLink = Url.Action("ResetPassword", "Account", new { area = "Identity", code = token, email = user.Email }, Request.Scheme);
            await _emailService.SendPasswordResetEmailAsync(user.Email ?? "", $"{user.FirstName} {user.LastName}", resetLink ?? "");

            _logger.LogInformation("Admin {AdminId} sent password reset email to {UserId} ({Email})",
                _userManager.GetUserId(User), userId, user.Email);

            TempData["Success"] = $"Password reset email sent to {user.Email}.";
            return RedirectToAction(nameof(UserDetail), new { id = userId });
        }

        // GET: /Admin/FeeSchedule
        public async Task<IActionResult> FeeSchedule()
        {
            var fees = await _context.SystemFeeConfigs
                .OrderBy(f => f.Id)
                .ToListAsync();

            var viewModel = new FeeScheduleViewModel { Fees = fees };
            return View(viewModel);
        }

        // POST: /Admin/UpdateFeeConfig
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateFeeConfig(SystemFeeConfigEditModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Validation failed. Please check the values and try again.";
                return RedirectToAction(nameof(FeeSchedule));
            }

            var fee = await _context.SystemFeeConfigs.FindAsync(model.Id);
            if (fee == null)
                return NotFound();

            var oldAmount = fee.Amount;
            var oldHSTApplicable = fee.HSTApplicable;
            var oldHSTIncluded = fee.HSTIncluded;

            fee.Amount = model.Amount;
            fee.HSTApplicable = model.HSTApplicable;
            fee.HSTIncluded = model.HSTIncluded;
            fee.Notes = model.Notes;
            fee.UpdatedAt = DateTime.UtcNow;
            fee.UpdatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = fee.UpdatedByUserId ?? "",
                Action = "UpdateFeeConfig",
                EntityType = "SystemFeeConfig",
                EntityId = fee.Id,
                OldValues = $"Amount={oldAmount}, HSTApplicable={oldHSTApplicable}, HSTIncluded={oldHSTIncluded}",
                NewValues = $"Amount={model.Amount}, HSTApplicable={model.HSTApplicable}, HSTIncluded={model.HSTIncluded}",
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminId} updated fee {FeeKey}: ${OldAmt} → ${NewAmt}",
                fee.UpdatedByUserId, fee.Key, oldAmount, model.Amount);

            TempData["Success"] = $"{fee.DisplayName} updated successfully.";
            return RedirectToAction(nameof(FeeSchedule));
        }

        // =============================================
        // CRUD: EditUser
        // =============================================

        // GET: /Admin/EditUser/userId
        public async Task<IActionResult> EditUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // SuperAdmin protection
            if (await IsTargetSuperAdmin(id) && !IsCurrentUserSuperAdmin())
            {
                TempData["Error"] = "Only Super Admins can edit Super Admin accounts.";
                return RedirectToAction(nameof(UserDetail), new { id });
            }

            var viewModel = new AdminEditUserViewModel
            {
                UserId = user.Id,
                Email = user.Email ?? "",
                FirstName = user.FirstName,
                LastName = user.LastName,
                Phone = user.PhoneNumber,
                CompanyName = user.CompanyName,
                UserType = user.UserType,
                IsActive = user.IsActive
            };

            return View(viewModel);
        }

        // POST: /Admin/EditUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(AdminEditUserViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return NotFound();

            // SuperAdmin protection
            if (await IsTargetSuperAdmin(model.UserId) && !IsCurrentUserSuperAdmin())
            {
                TempData["Error"] = "Only Super Admins can edit Super Admin accounts.";
                return RedirectToAction(nameof(UserDetail), new { id = model.UserId });
            }

            // Cannot change someone TO SuperAdmin/Admin unless caller is SuperAdmin
            if ((model.UserType == UserType.PlatformAdmin) && !IsCurrentUserSuperAdmin())
            {
                ModelState.AddModelError("UserType", "Only Super Admins can assign the Admin role.");
                return View(model);
            }

            var oldUserType = user.UserType;
            user.Email = model.Email;
            user.UserName = model.Email;
            user.NormalizedEmail = model.Email.ToUpperInvariant();
            user.NormalizedUserName = model.Email.ToUpperInvariant();
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.PhoneNumber = model.Phone;
            user.CompanyName = model.CompanyName;
            user.UserType = model.UserType;
            user.IsActive = model.IsActive;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
                return View(model);
            }

            // Sync roles if UserType changed
            if (oldUserType != model.UserType)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                // Remove old role (keep SuperAdmin if it exists)
                var roleToRemove = GetRoleName(oldUserType);
                if (!string.IsNullOrEmpty(roleToRemove) && currentRoles.Contains(roleToRemove))
                    await _userManager.RemoveFromRoleAsync(user, roleToRemove);

                // Add new role
                var roleToAdd = GetRoleName(model.UserType);
                if (!string.IsNullOrEmpty(roleToAdd) && !currentRoles.Contains(roleToAdd))
                    await _userManager.AddToRoleAsync(user, roleToAdd);
            }

            _logger.LogInformation("Admin {AdminId} edited user {UserId}", _userManager.GetUserId(User), model.UserId);
            TempData["Success"] = $"User {user.Email} updated successfully.";
            return RedirectToAction(nameof(UserDetail), new { id = model.UserId });
        }

        // =============================================
        // CRUD: CreateUser
        // =============================================

        // GET: /Admin/CreateUser
        public IActionResult CreateUser()
        {
            return View(new AdminCreateUserViewModel());
        }

        // POST: /Admin/CreateUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(AdminCreateUserViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Admin CreateUser is restricted to Builder accounts only
            var newUser = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                PhoneNumber = model.Phone,
                CompanyName = model.CompanyName,
                UserType = UserType.Builder,
                MaxProjects = model.MaxProjects,
                CreatedByUserId = _userManager.GetUserId(User),
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(newUser, model.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
                return View(model);
            }

            // Assign Builder role
            await _userManager.AddToRoleAsync(newUser, "Builder");

            // Send invitation email if requested
            if (model.SendInvitation)
            {
                var loginLink = Url.Action("Login", "Account", new { area = "Identity" }, Request.Scheme);
                await _emailService.SendAdminCreatedUserEmailAsync(model.Email, $"{model.FirstName} {model.LastName}", "Builder", loginLink ?? "");
            }

            _logger.LogInformation("Admin {AdminId} created builder {Email} (MaxProjects={MaxProjects})",
                _userManager.GetUserId(User), model.Email, model.MaxProjects);

            TempData["Success"] = $"Builder {model.Email} created successfully.";
            return RedirectToAction(nameof(UserDetail), new { id = newUser.Id });
        }

        // =============================================
        // CRUD: DeleteUser
        // =============================================

        // GET: /Admin/DeleteUser/userId
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // SuperAdmin protection
            if (await IsTargetSuperAdmin(id) && !IsCurrentUserSuperAdmin())
            {
                TempData["Error"] = "Only Super Admins can delete Super Admin accounts.";
                return RedirectToAction(nameof(UserDetail), new { id });
            }

            var (hasActivity, projectCount, unitCount, assignmentCount) = await CheckUserActivity(user);

            var viewModel = new AdminDeleteUserViewModel
            {
                UserId = user.Id,
                FullName = $"{user.FirstName} {user.LastName}",
                Email = user.Email ?? "",
                UserType = user.UserType,
                ProjectCount = projectCount,
                UnitCount = unitCount,
                AssignmentCount = assignmentCount,
                HasAnyActivity = hasActivity
            };

            return View(viewModel);
        }

        // POST: /Admin/DeleteUserConfirmed
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUserConfirmed(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            // SuperAdmin protection
            if (await IsTargetSuperAdmin(userId) && !IsCurrentUserSuperAdmin())
            {
                TempData["Error"] = "Only Super Admins can delete Super Admin accounts.";
                return RedirectToAction(nameof(Users));
            }

            var (hasActivity, _, _, _) = await CheckUserActivity(user);
            if (hasActivity)
            {
                TempData["Error"] = "Cannot delete this user because they have active projects or activity. Deactivate the account instead.";
                return RedirectToAction(nameof(DeleteUser), new { id = userId });
            }

            var email = user.Email;
            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                _logger.LogWarning("Admin {AdminId} permanently deleted user {Email}",
                    _userManager.GetUserId(User), email);
                TempData["Success"] = $"User {email} has been permanently deleted.";
                return RedirectToAction(nameof(Users));
            }

            foreach (var error in result.Errors)
                TempData["Error"] = error.Description;

            return RedirectToAction(nameof(DeleteUser), new { id = userId });
        }

        // =============================================
        // Helper: Check user activity for deletion
        // =============================================
        private async Task<(bool HasActivity, int ProjectCount, int UnitCount, int AssignmentCount)> CheckUserActivity(ApplicationUser user)
        {
            int projectCount = 0, unitCount = 0, assignmentCount = 0;
            bool hasActivity = false;

            switch (user.UserType)
            {
                case UserType.Builder:
                    projectCount = await _context.Projects.CountAsync(p => p.BuilderId == user.Id);
                    unitCount = await _context.Units
                        .Include(u => u.Project)
                        .CountAsync(u => u.Project.BuilderId == user.Id);
                    hasActivity = projectCount > 0 || unitCount > 0
                        || await _context.AuditLogs.AnyAsync(a => a.UserId == user.Id);
                    break;

                case UserType.Purchaser:
                    unitCount = await _context.UnitPurchasers.CountAsync(up => up.PurchaserId == user.Id);
                    hasActivity = unitCount > 0
                        || await _context.Documents.AnyAsync(d => d.UploadedById == user.Id);
                    break;

                case UserType.Lawyer:
                    assignmentCount = await _context.LawyerAssignments.CountAsync(la => la.LawyerId == user.Id);
                    hasActivity = assignmentCount > 0;
                    break;

                case UserType.MarketingAgency:
                    hasActivity = await _context.AuditLogs.AnyAsync(a => a.UserId == user.Id);
                    break;

                case UserType.PlatformAdmin:
                    hasActivity = await _context.AuditLogs.AnyAsync(a => a.UserId == user.Id && a.Action != "Login");
                    break;
            }

            return (hasActivity, projectCount, unitCount, assignmentCount);
        }

        // =============================================
        // Builder Quotas
        // =============================================

        // GET: /Admin/SetBuilderQuota/userId
        public async Task<IActionResult> SetBuilderQuota(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || user.UserType != UserType.Builder)
                return NotFound();

            var projects = await _context.Projects
                .Where(p => p.BuilderId == id)
                .Select(p => new AdminProjectQuotaItem
                {
                    ProjectId = p.Id,
                    ProjectName = p.Name,
                    CurrentUnitCount = p.Units.Count,
                    MaxUnits = p.MaxUnits
                }).ToListAsync();

            var viewModel = new AdminSetBuilderQuotaViewModel
            {
                UserId = user.Id,
                BuilderName = $"{user.FirstName} {user.LastName}",
                MaxProjects = user.MaxProjects,
                Projects = projects
            };

            return View(viewModel);
        }

        // POST: /Admin/SetBuilderQuota
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetBuilderQuota(AdminSetBuilderQuotaViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null || user.UserType != UserType.Builder)
                return NotFound();

            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Update MaxProjects on user
            var oldMaxProjects = user.MaxProjects;
            user.MaxProjects = model.MaxProjects;
            await _userManager.UpdateAsync(user);

            // Update MaxUnits on each project
            foreach (var item in model.Projects)
            {
                var project = await _context.Projects.FindAsync(item.ProjectId);
                if (project != null && project.BuilderId == model.UserId)
                {
                    var oldMaxUnits = project.MaxUnits;
                    project.MaxUnits = item.MaxUnits;

                    if (oldMaxUnits != item.MaxUnits)
                    {
                        _context.AuditLogs.Add(new AuditLog
                        {
                            UserId = adminId ?? "",
                            Action = "UpdateProjectQuota",
                            EntityType = "Project",
                            EntityId = project.Id,
                            OldValues = $"MaxUnits={oldMaxUnits}",
                            NewValues = $"MaxUnits={item.MaxUnits}",
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
            }

            if (oldMaxProjects != model.MaxProjects)
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = adminId ?? "",
                    Action = "UpdateBuilderQuota",
                    EntityType = "ApplicationUser",
                    EntityId = 0,
                    OldValues = $"MaxProjects={oldMaxProjects}",
                    NewValues = $"MaxProjects={model.MaxProjects}",
                    Comments = $"Builder: {user.Email}",
                    Timestamp = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminId} updated quotas for builder {BuilderId}: MaxProjects={MaxProjects}",
                adminId, model.UserId, model.MaxProjects);

            TempData["Success"] = $"Quotas updated for {user.FirstName} {user.LastName}.";
            return RedirectToAction(nameof(UserDetail), new { id = model.UserId });
        }

        // =============================================
        // Helper: Map UserType to role name
        // =============================================
        private static string? GetRoleName(UserType userType)
        {
            return userType switch
            {
                UserType.PlatformAdmin => "Admin",
                UserType.Builder => "Builder",
                UserType.Purchaser => "Purchaser",
                UserType.Lawyer => "Lawyer",
                UserType.MarketingAgency => "MarketingAgency",
                _ => null
            };
        }
    }
}
