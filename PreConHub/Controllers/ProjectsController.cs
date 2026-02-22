using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PreConHub.Data;
using PreConHub.Models.Entities;
using PreConHub.Models.ViewModels;
using PreConHub.Services;
using ClosedXML.Excel;

namespace PreConHub.Controllers
{
    [Authorize(Roles = "Admin,Builder")]
    public class ProjectsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProjectSummaryService _summaryService;
        private readonly IShortfallAnalysisService _shortfallService;
        private readonly ILogger<ProjectsController> _logger;

        private readonly IEmailService _emailService;

        public ProjectsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IProjectSummaryService summaryService,
            IShortfallAnalysisService shortfallService,
            IEmailService emailService,
            ILogger<ProjectsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _summaryService = summaryService;
            _shortfallService = shortfallService;
            _emailService = emailService;  
            _logger = logger;
        }

        // GET: /Projects
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            var projectsQuery = _context.Projects
                .Include(p => p.Units)
                .AsQueryable();

            // Non-admin users only see their own projects
            if (!isAdmin)
            {
                projectsQuery = projectsQuery.Where(p => p.BuilderId == userId);
            }

            var projects = await projectsQuery
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            // Load builder quota info
            var builderUser = userId != null ? await _userManager.FindByIdAsync(userId) : null;
            var maxProjects = builderUser?.MaxProjects ?? 99;
            var canCreate = isAdmin || projects.Count < maxProjects;

            var viewModel = new ProjectListViewModel
            {
                TotalProjects = projects.Count,
                ActiveProjects = projects.Count(p => p.Status == ProjectStatus.Active),
                MaxProjects = maxProjects,
                CanCreateProject = canCreate,
                Projects = projects.Select(p => new ProjectItemViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Address = p.Address,
                    City = p.City,
                    TotalUnits = p.Units.Count,
                    Status = p.Status,
                    ClosingDate = p.ClosingDate,
                    UnitsReadyToClose = p.Units.Count(u => u.Recommendation == ClosingRecommendation.ProceedToClose),
                    UnitsAtRisk = p.Units.Count(u => u.Recommendation == ClosingRecommendation.HighRiskDefault 
                                                   || u.Recommendation == ClosingRecommendation.PotentialDefault),
                    ClosingProbability = p.Units.Count > 0 
                        ? Math.Round((decimal)p.Units.Count(u => 
                            u.Recommendation == ClosingRecommendation.ProceedToClose ||
                            u.Recommendation == ClosingRecommendation.CloseWithDiscount ||
                            u.Recommendation == ClosingRecommendation.VTBSecondMortgage ||
                            u.Recommendation == ClosingRecommendation.VTBFirstMortgage) 
                            / p.Units.Count * 100, 1)
                        : 0
                }).ToList()
            };

            return View(viewModel);
        }

        // GET: /Projects/Dashboard/5
        public async Task<IActionResult> Dashboard(int id)
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            var project = await _context.Projects
                .Include(p => p.Units)
                    .ThenInclude(u => u.Purchasers)
                        .ThenInclude(up => up.MortgageInfo)
                .Include(p => p.Units)
                    .ThenInclude(u => u.Deposits)
                .Include(p => p.Units)
                    .ThenInclude(u => u.SOA)
                .Include(p => p.Units)
                    .ThenInclude(u => u.ShortfallAnalysis)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
            {
                return NotFound();
            }

            // Security check
            if (!isAdmin && project.BuilderId != userId)
            {
                return Forbid();
            }

            // Expose marketing access flag to view
            ViewBag.AllowMarketingAccess = project.AllowMarketingAccess;

            // Calculate/refresh project summary
            var summary = await _summaryService.CalculateProjectSummaryAsync(id);

            var viewModel = new ProjectDashboardViewModel
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                ProjectAddress = $"{project.Address}, {project.City}",
                Summary = new ProjectSummaryViewModel
                {
                    TotalUnits = summary.TotalUnits,
                    UnitsReadyToClose = summary.UnitsReadyToClose,
                    UnitsNeedingDiscount = summary.UnitsNeedingDiscount,
                    UnitsNeedingVTB = summary.UnitsNeedingVTB,
                    UnitsAtRisk = summary.UnitsAtRisk,
                    UnitsPendingData = summary.UnitsPendingData,
                    PercentReadyToClose = summary.PercentReadyToClose,
                    PercentNeedingDiscount = summary.PercentNeedingDiscount,
                    PercentNeedingVTB = summary.PercentNeedingVTB,
                    PercentAtRisk = summary.PercentAtRisk,
                    TotalSalesValue = summary.TotalSalesValue,
                    TotalDiscountRequired = summary.TotalDiscountRequired,
                    DiscountPercentOfSales = summary.DiscountPercentOfSales,
                    TotalInvestmentAtRisk = summary.TotalInvestmentAtRisk,
                    TotalShortfall = summary.TotalShortfall,
                    ClosingProbabilityPercent = summary.ClosingProbabilityPercent
                },
                Units = project.Units.OrderBy(u => u.UnitNumber).Select(u => new UnitRowViewModel
                {
                    UnitId = u.Id,
                    UnitNumber = u.UnitNumber,
                    HasMortgageApproval = u.Purchasers
                        .Any(p => p.MortgageInfo != null && p.MortgageInfo.HasMortgageApproval),
                    MortgageProvider = u.Purchasers
                        .FirstOrDefault(p => p.IsPrimaryPurchaser)?.MortgageInfo?.MortgageProvider,
                    MortgageAmount = u.Purchasers
                        .FirstOrDefault(p => p.IsPrimaryPurchaser)?.MortgageInfo?.ApprovedAmount ?? 0,
                    IsApprovedAtClosing = u.Purchasers
                        .Any(p => p.MortgageInfo != null && p.MortgageInfo.IsApprovalConfirmed),
                    AppraisalValue = u.CurrentAppraisalValue,
                    SOAAmount = u.SOA?.BalanceDueOnClosing ?? 0,
                    TotalPaid = u.Deposits.Where(d => d.IsPaid).Sum(d => d.Amount),
                    ShortfallAmount = u.ShortfallAnalysis?.ShortfallAmount ?? 0,
                    ShortfallPercent = u.ShortfallAnalysis?.ShortfallPercentage ?? 0,
                    Recommendation = u.Recommendation ?? ClosingRecommendation.ProceedToClose
                }).ToList(),
                TotalIncomeDueClosing = project.Units
                    .Where(u => u.SOA != null)
                    .Sum(u => u.SOA!.CashRequiredToClose),
                MaxUnits = project.MaxUnits,
                CurrentUnitCount = project.Units.Count,
                CanAddUnit = isAdmin || (project.MaxUnits.HasValue && project.Units.Count < project.MaxUnits.Value)
            };

            return View(viewModel);
        }

        // GET: /Projects/Create
        public IActionResult Create()
        {
            return View(new CreateProjectViewModel());
        }

        // POST: /Projects/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateProjectViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userId = _userManager.GetUserId(User);

            // Quota enforcement: non-admin builders check MaxProjects
            if (!User.IsInRole("Admin"))
            {
                var builder = await _userManager.FindByIdAsync(userId!);
                if (builder != null)
                {
                    var currentProjectCount = await _context.Projects.CountAsync(p => p.BuilderId == userId);
                    if (currentProjectCount >= builder.MaxProjects)
                    {
                        TempData["Error"] = $"You have reached your project limit ({builder.MaxProjects}). Contact an administrator to increase your quota.";
                        return RedirectToAction(nameof(Index));
                    }
                }
            }

            var project = new Project
            {
                Name = model.Name,
                Address = model.Address,
                City = model.City,
                PostalCode = model.PostalCode,
                ProjectType = model.ProjectType,
                TotalUnits = model.TotalUnits,
                OccupancyDate = model.OccupancyDate,
                ClosingDate = model.ClosingDate,
                BuilderId = userId!,
                Status = ProjectStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "Project",
                EntityId = project.Id,
                Action = "Create",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = User.IsInRole("Admin") ? "Admin" : "Builder",
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { project.Name, project.City, project.TotalUnits }),
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            _logger.LogInformation("Project {ProjectName} created by {UserId}", project.Name, userId);

            TempData["Success"] = $"Project '{project.Name}' created successfully.";
            return RedirectToAction(nameof(Dashboard), new { id = project.Id });
        }

        // GET: /Projects/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var project = await _context.Projects.FindAsync(id);
            
            if (project == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && project.BuilderId != userId)
            {
                return Forbid();
            }

            var viewModel = new CreateProjectViewModel
            {
                Name = project.Name,
                Address = project.Address,
                City = project.City,
                PostalCode = project.PostalCode,
                ProjectType = project.ProjectType,
                TotalUnits = project.TotalUnits,
                OccupancyDate = project.OccupancyDate,
                ClosingDate = project.ClosingDate
            };

            return View(viewModel);
        }

        // POST: /Projects/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CreateProjectViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var project = await _context.Projects.FindAsync(id);
            
            if (project == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && project.BuilderId != userId)
            {
                return Forbid();
            }

            project.Name = model.Name;
            project.Address = model.Address;
            project.City = model.City;
            project.PostalCode = model.PostalCode;
            project.ProjectType = model.ProjectType;
            project.TotalUnits = model.TotalUnits;
            project.OccupancyDate = model.OccupancyDate;
            project.ClosingDate = model.ClosingDate;
            project.UpdatedAt = DateTime.UtcNow;

            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "Project",
                EntityId = project.Id,
                Action = "Edit",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = User.IsInRole("Admin") ? "Admin" : "Builder",
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { project.Name, project.City, project.ClosingDate }),
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Project updated successfully.";
            return RedirectToAction(nameof(Dashboard), new { id });
        }

        // POST: /Projects/RefreshAnalysis/5
        [HttpGet]
        public async Task<IActionResult> RefreshAnalysis(int id)
        {
            var project = await _context.Projects
                .Include(p => p.Units)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && project.BuilderId != userId)
            {
                return Forbid();
            }

            // Recalculate shortfall for all units
            foreach (var unit in project.Units)
            {
                try
                {
                    await _shortfallService.RecalculateShortfallAsync(unit.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to recalculate shortfall for unit {UnitId}", unit.Id);
                }
            }

            // Refresh project summary
            await _summaryService.CalculateProjectSummaryAsync(id);

            TempData["Success"] = "All analyses refreshed successfully.";
            return RedirectToAction(nameof(Dashboard), new { id });
        }

        // GET: /Projects/AddFees/5
        public async Task<IActionResult> AddFees(int id)
        {
            var project = await _context.Projects
                .Include(p => p.Fees)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
            {
                return NotFound();
            }

            return View(project);
        }

        // POST: /Projects/AddFee
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFee(int projectId, string feeName, FeeType feeType, decimal amount, string? description)
        {
            var project = await _context.Projects.FindAsync(projectId);

            if (project == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);
            var fee = new ProjectFee
            {
                ProjectId = projectId,
                FeeName = feeName,
                FeeType = feeType,
                Amount = amount,
                Description = description,
                AppliesToAllUnits = true
            };

            _context.ProjectFees.Add(fee);
            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "ProjectFee",
                EntityId = projectId,
                Action = "Create",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = User.IsInRole("Admin") ? "Admin" : "Builder",
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { feeName, feeType, amount }),
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Fee added successfully.";
            return RedirectToAction(nameof(AddFees), new { id = projectId });
        }

        public async Task<IActionResult> Purchasers(int id)
        {
            var userId = _userManager.GetUserId(User);

            var project = await _context.Projects
                .Include(p => p.Units)
                    .ThenInclude(u => u.Purchasers)
                        .ThenInclude(up => up.Purchaser)
                .Include(p => p.Units)
                    .ThenInclude(u => u.Purchasers)
                        .ThenInclude(up => up.MortgageInfo)
                .Include(p => p.Units)
                    .ThenInclude(u => u.Purchasers)
                        .ThenInclude(up => up.Financials)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            if (!User.IsInRole("Admin") && project.BuilderId != userId)
                return Forbid();

            var purchasers = new List<PurchaserListItemViewModel>();

            foreach (var unit in project.Units)
            {
                foreach (var up in unit.Purchasers)
                {
                    purchasers.Add(new PurchaserListItemViewModel
                    {
                        UnitPurchaserId = up.Id,
                        UserId = up.PurchaserId,
                        FirstName = up.Purchaser.FirstName,
                        LastName = up.Purchaser.LastName,
                        Email = up.Purchaser.Email,
                        Phone = up.Purchaser.PhoneNumber,
                        IsPrimary = up.IsPrimaryPurchaser,

                        UnitId = unit.Id,
                        UnitNumber = unit.UnitNumber,
                        PurchasePrice = unit.PurchasePrice,

                        HasActivated = up.Purchaser.EmailConfirmed,
                        LastLoginAt = up.Purchaser.LastLoginAt,
                        AddedAt = up.CreatedAt,

                        HasSubmittedMortgageInfo = up.MortgageInfo != null,
                        MortgageApproved = up.MortgageInfo?.HasMortgageApproval ?? false,
                        MortgageAmount = up.MortgageInfo?.ApprovedAmount ?? 0,

                        HasSubmittedFinancials = up.Financials != null,
                        TotalFundsAvailable = up.Financials?.TotalFundsAvailable ?? 0
                    });
                }
            }

            var viewModel = new ProjectPurchasersViewModel
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                TotalUnits = project.Units.Count,
                Purchasers = purchasers.OrderBy(p => p.UnitNumber).ThenBy(p => !p.IsPrimary).ToList()
            };

            return View(viewModel);
        }

        // GET: /Projects/Lawyers/5
        public async Task<IActionResult> Lawyers(int id)
        {
            var userId = _userManager.GetUserId(User);

            var project = await _context.Projects
                .Include(p => p.Units)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            if (!User.IsInRole("Admin") && project.BuilderId != userId)
                return Forbid();

            // Get all lawyer assignments for this project
            var assignments = await _context.LawyerAssignments
                .Include(la => la.Lawyer)
                .Include(la => la.Unit)
                .Where(la => la.Unit.ProjectId == id && la.IsActive)
                .ToListAsync();

            // Group by lawyer
            var lawyerGroups = assignments
                .GroupBy(a => a.LawyerId)
                .Select(g => new LawyerListItemViewModel
                {
                    LawyerId = g.Key,
                    FirstName = g.First().Lawyer.FirstName,
                    LastName = g.First().Lawyer.LastName,
                    Email = g.First().Lawyer.Email,
                    Phone = g.First().Lawyer.PhoneNumber,
                    LawFirm = g.First().Lawyer.CompanyName,
                    HasActivated = g.First().Lawyer.EmailConfirmed,
                    LastLoginAt = g.First().Lawyer.LastLoginAt,
                    AssignedUnitsCount = g.Count(),
                    AssignedUnitNumbers = g.Select(a => a.Unit.UnitNumber).OrderBy(n => n).ToList(),
                    PendingCount = g.Count(a => a.ReviewStatus == LawyerReviewStatus.Pending),
                    UnderReviewCount = g.Count(a => a.ReviewStatus == LawyerReviewStatus.UnderReview),
                    ApprovedCount = g.Count(a => a.ReviewStatus == LawyerReviewStatus.Approved),
                    NeedsRevisionCount = g.Count(a => a.ReviewStatus == LawyerReviewStatus.NeedsRevision),
                    Assignments = g.Select(a => new LawyerAssignmentDetailViewModel
                    {
                        AssignmentId = a.Id,
                        UnitId = a.UnitId ?? 0,
                        UnitNumber = a.Unit.UnitNumber,
                        ReviewStatus = a.ReviewStatus,
                        AssignedAt = a.AssignedAt,
                        ReviewedAt = a.ReviewedAt
                    }).OrderBy(a => a.UnitNumber).ToList()
                })
                .OrderBy(l => l.LastName)
                .ToList();

            var viewModel = new ProjectLawyersViewModel
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                TotalUnits = project.Units.Count,
                Lawyers = lawyerGroups
            };

            return View(viewModel);
        }

        // POST: /Projects/ResendLawyerInvitation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendLawyerInvitation(string lawyerId, int projectId)
        {
            var userId = _userManager.GetUserId(User);

            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
                return NotFound();

            if (!User.IsInRole("Admin") && project.BuilderId != userId)
                return Forbid();

            var lawyer = await _userManager.FindByIdAsync(lawyerId);
            if (lawyer == null)
            {
                TempData["Error"] = "Lawyer not found.";
                return RedirectToAction(nameof(Lawyers), new { id = projectId });
            }

            if (lawyer.EmailConfirmed)
            {
                TempData["Warning"] = $"{lawyer.FirstName} {lawyer.LastName} has already activated their account.";
                return RedirectToAction(nameof(Lawyers), new { id = projectId });
            }

            // Generate new token
            var token = await _userManager.GeneratePasswordResetTokenAsync(lawyer);
            var encodedToken = System.Net.WebUtility.UrlEncode(token);

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var invitationLink = $"{baseUrl}/Lawyer/AcceptInvitation?email={System.Net.WebUtility.UrlEncode(lawyer.Email!)}&code={encodedToken}";

            // Get assignment count for this lawyer in this project
            var assignmentCount = await _context.LawyerAssignments
                .CountAsync(la => la.LawyerId == lawyerId && la.Unit.ProjectId == projectId && la.IsActive);

            var emailSent = await _emailService.SendLawyerInvitationAsync(
                lawyer.Email!,
                $"{lawyer.FirstName} {lawyer.LastName}",
                assignmentCount,
                new List<string> { project.Name },
                invitationLink
            );

            TempData["EmailSent"] = emailSent;

            TempData["InvitationLink"] = invitationLink;
            TempData["Success"] = $"New invitation link generated for {lawyer.FirstName} {lawyer.LastName}.";

            _logger.LogInformation("Lawyer invitation regenerated for {Email}", lawyer.Email);

            return RedirectToAction(nameof(Lawyers), new { id = projectId });
        }

        // GET: /Projects/BulkAssignLawyer/5
        public async Task<IActionResult> BulkAssignLawyer(int id)
        {
            var userId = _userManager.GetUserId(User);

            var project = await _context.Projects
                .Include(p => p.Units)
                    .ThenInclude(u => u.Purchasers)
                        .ThenInclude(up => up.Purchaser)
                .Include(p => p.Units)
                    .ThenInclude(u => u.LawyerAssignments)
                        .ThenInclude(la => la.Lawyer)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            if (!User.IsInRole("Admin") && project.BuilderId != userId)
                return Forbid();

            // Get all lawyers in the system
            var allLawyers = await _userManager.GetUsersInRoleAsync("Lawyer");

            var viewModel = new BulkAssignLawyerViewModel
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                ExistingLawyers = allLawyers.OrderBy(l => l.LastName).ToList(),
                Units = project.Units.OrderBy(u => u.UnitNumber).Select(u => new BulkAssignUnitViewModel
                {
                    UnitId = u.Id,
                    UnitNumber = u.UnitNumber,
                    UnitType = u.UnitType.ToString(),
                    PurchasePrice = u.PurchasePrice,
                    PurchaserName = u.Purchasers
                        .Where(p => p.IsPrimaryPurchaser)
                        .Select(p => $"{p.Purchaser.FirstName} {p.Purchaser.LastName}")
                        .FirstOrDefault(),
                    HasLawyer = u.LawyerAssignments.Any(la => la.IsActive),
                    LawyerConfirmed = u.LawyerConfirmed,
                    AssignedLawyers = u.LawyerAssignments
                        .Where(la => la.IsActive)
                        .Select(la => $"{la.Lawyer.FirstName} {la.Lawyer.LastName}")
                        .ToList()
                }).ToList()
            };

            return View(viewModel);
        }

        // POST: /Projects/BulkAssignLawyer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkAssignLawyer(
            int projectId,
            string lawyerType,
            string? existingLawyerId,
            string? lawyerFirstName,
            string? lawyerLastName,
            string? lawyerEmail,
            string? lawyerPhone,
            string? lawFirm,
            List<int> selectedUnitIds,
            bool skipIfSameLawyer = true,
            bool sendInvitation = true)
        {
            var userId = _userManager.GetUserId(User);

            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
                return NotFound();

            if (!User.IsInRole("Admin") && project.BuilderId != userId)
                return Forbid();

            if (selectedUnitIds == null || !selectedUnitIds.Any())
            {
                TempData["Error"] = "Please select at least one unit.";
                return RedirectToAction(nameof(BulkAssignLawyer), new { id = projectId });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                ApplicationUser? lawyerUser = null;
                bool isNewLawyer = false;

                // Get or create lawyer
                if (lawyerType == "existing" && !string.IsNullOrEmpty(existingLawyerId))
                {
                    lawyerUser = await _userManager.FindByIdAsync(existingLawyerId);
                    if (lawyerUser == null)
                    {
                        TempData["Error"] = "Selected lawyer not found.";
                        return RedirectToAction(nameof(BulkAssignLawyer), new { id = projectId });
                    }
                }
                else if (lawyerType == "new")
                {
                    if (string.IsNullOrWhiteSpace(lawyerEmail) || string.IsNullOrWhiteSpace(lawyerFirstName) || string.IsNullOrWhiteSpace(lawyerLastName))
                    {
                        TempData["Error"] = "Please provide lawyer's name and email.";
                        return RedirectToAction(nameof(BulkAssignLawyer), new { id = projectId });
                    }

                    // Check if email already exists
                    lawyerUser = await _userManager.FindByEmailAsync(lawyerEmail);

                    if (lawyerUser == null)
                    {
                        lawyerUser = new ApplicationUser
                        {
                            UserName = lawyerEmail,
                            Email = lawyerEmail,
                            FirstName = lawyerFirstName,
                            LastName = lawyerLastName,
                            PhoneNumber = lawyerPhone,
                            CompanyName = lawFirm,
                            UserType = UserType.Lawyer,
                            CreatedByUserId = userId,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            EmailConfirmed = false
                        };

                        var tempPassword = GenerateTemporaryPassword();
                        var createResult = await _userManager.CreateAsync(lawyerUser, tempPassword);

                        if (!createResult.Succeeded)
                        {
                            var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                            TempData["Error"] = $"Failed to create lawyer account: {errors}";
                            return RedirectToAction(nameof(BulkAssignLawyer), new { id = projectId });
                        }

                        await _userManager.AddToRoleAsync(lawyerUser, "Lawyer");
                        isNewLawyer = true;
                    }
                }
                else
                {
                    TempData["Error"] = "Please select or create a lawyer.";
                    return RedirectToAction(nameof(BulkAssignLawyer), new { id = projectId });
                }

                // Get selected units
                var units = await _context.Units
                    .Include(u => u.LawyerAssignments)
                    .Where(u => selectedUnitIds.Contains(u.Id) && u.ProjectId == projectId)
                    .ToListAsync();

                int assignedCount = 0;
                int skippedCount = 0;

                foreach (var unit in units)
                {
                    // Check if this lawyer is already assigned
                    if (skipIfSameLawyer)
                    {
                        var alreadyAssigned = unit.LawyerAssignments
                            .Any(la => la.LawyerId == lawyerUser!.Id && la.IsActive);

                        if (alreadyAssigned)
                        {
                            skippedCount++;
                            continue;
                        }
                    }

                    // Create new assignment
                    var assignment = new LawyerAssignment
                    {
                        UnitId = unit.Id,
                        ProjectId = projectId,
                        LawyerId = lawyerUser!.Id,
                        AssignedAt = DateTime.UtcNow,
                        IsActive = true,
                        ReviewStatus = LawyerReviewStatus.Pending
                    };

                    _context.LawyerAssignments.Add(assignment);
                    assignedCount++;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Generate invitation link for new lawyer
                string? invitationLink = null;
                if (isNewLawyer && sendInvitation && !lawyerUser!.EmailConfirmed)
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(lawyerUser);
                    var encodedToken = System.Net.WebUtility.UrlEncode(token);

                    var baseUrl = $"{Request.Scheme}://{Request.Host}";
                    invitationLink = $"{baseUrl}/Lawyer/AcceptInvitation?email={System.Net.WebUtility.UrlEncode(lawyerUser.Email!)}&code={encodedToken}";

                    TempData["InvitationLink"] = invitationLink;

                    // ADD THIS: Send invitation email
                    var emailSent = await _emailService.SendLawyerInvitationAsync(
                        lawyerUser.Email!,
                        $"{lawyerUser.FirstName} {lawyerUser.LastName}",
                        assignedCount,
                        new List<string> { project.Name },
                        invitationLink
                    );

                    if (emailSent)
                    {
                        _logger.LogInformation("Lawyer invitation email sent to {Email} for {Count} units", lawyerUser.Email, assignedCount);
                    }

                    TempData["EmailSent"] = emailSent;
                }

                _logger.LogInformation("Lawyer {LawyerId} assigned to {Count} units in project {ProjectId}",
                    lawyerUser!.Id, assignedCount, projectId);

                var message = $"Assigned {assignedCount} unit(s) to {lawyerUser.FirstName} {lawyerUser.LastName}";
                if (skippedCount > 0)
                {
                    message += $" ({skippedCount} skipped - already assigned)";
                }
                TempData["Success"] = message;

                return RedirectToAction(nameof(Lawyers), new { id = projectId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error bulk assigning lawyer to project {ProjectId}", projectId);
                TempData["Error"] = $"An error occurred: {ex.Message}";
                return RedirectToAction(nameof(BulkAssignLawyer), new { id = projectId });
            }
        }

        // POST: /Projects/ToggleMarketingAccess/5
        // Builder grants or revokes Marketing Agency access for a project (spec Section H)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleMarketingAccess(int id)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && project.BuilderId != userId)
                return Forbid();

            project.AllowMarketingAccess = !project.AllowMarketingAccess;
            project.UpdatedAt = DateTime.UtcNow;

            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "Project",
                EntityId = project.Id,
                Action = project.AllowMarketingAccess ? "MarketingAccessGranted" : "MarketingAccessRevoked",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = User.IsInRole("Admin") ? "Admin" : "Builder",
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { AllowMarketingAccess = project.AllowMarketingAccess }),
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = project.AllowMarketingAccess
                ? "Marketing Agency access granted for this project."
                : "Marketing Agency access revoked for this project.";

            return RedirectToAction(nameof(Dashboard), new { id });
        }

        // GET: /Projects/ManageMarketingAccess/5
        // Per-project MA user assignment page (spec Section H)
        public async Task<IActionResult> ManageMarketingAccess(int id)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin") && project.BuilderId != userId)
                return Forbid();

            var maUsers = await _userManager.GetUsersInRoleAsync("MarketingAgency");

            ViewBag.ProjectId = id;
            ViewBag.ProjectName = project.Name;
            ViewBag.AllowMarketingAccess = project.AllowMarketingAccess;
            ViewBag.MarketingAgencyUserId = project.MarketingAgencyUserId;
            ViewBag.MAUsers = maUsers.Where(u => u.IsActive).OrderBy(u => u.FirstName).ToList();

            return View();
        }

        // POST: /Projects/ManageMarketingAccess/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageMarketingAccess(int id, bool allowAccess, string? marketingAgencyUserId)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin") && project.BuilderId != userId)
                return Forbid();

            project.AllowMarketingAccess = allowAccess;
            project.MarketingAgencyUserId = allowAccess ? marketingAgencyUserId : null;
            project.UpdatedAt = DateTime.UtcNow;

            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "Project",
                EntityId = project.Id,
                Action = "ManageMarketingAccess",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = User.IsInRole("Admin") ? "Admin" : "Builder",
                NewValues = System.Text.Json.JsonSerializer.Serialize(new
                {
                    AllowMarketingAccess = project.AllowMarketingAccess,
                    MarketingAgencyUserId = project.MarketingAgencyUserId
                }),
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = allowAccess
                ? "Marketing Agency access configured successfully."
                : "Marketing Agency access revoked.";

            return RedirectToAction(nameof(Dashboard), new { id });
        }

        // POST: /Projects/CreateMarketingAgencyUser/5
        // Builder creates a new MA user inline from ManageMarketingAccess page
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMarketingAgencyUser(
            int id,
            string maFirstName,
            string maLastName,
            string maEmail,
            string? maCompanyName)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin") && project.BuilderId != userId)
                return Forbid();

            if (string.IsNullOrWhiteSpace(maEmail) || string.IsNullOrWhiteSpace(maFirstName) || string.IsNullOrWhiteSpace(maLastName))
            {
                TempData["Error"] = "First name, last name, and email are required.";
                return RedirectToAction(nameof(ManageMarketingAccess), new { id });
            }

            // Check if user with this email already exists
            var existingUser = await _userManager.FindByEmailAsync(maEmail);
            if (existingUser != null)
            {
                // If they're already an MA user, just redirect back — they'll appear in the dropdown
                if (await _userManager.IsInRoleAsync(existingUser, "MarketingAgency"))
                {
                    TempData["Success"] = $"{existingUser.FirstName} {existingUser.LastName} already exists. Select them from the dropdown below.";
                    return RedirectToAction(nameof(ManageMarketingAccess), new { id });
                }

                TempData["Error"] = $"A user with email {maEmail} already exists with a different role.";
                return RedirectToAction(nameof(ManageMarketingAccess), new { id });
            }

            var maUser = new ApplicationUser
            {
                UserName = maEmail,
                Email = maEmail,
                FirstName = maFirstName,
                LastName = maLastName,
                CompanyName = maCompanyName,
                UserType = UserType.MarketingAgency,
                CreatedByUserId = userId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                EmailConfirmed = false
            };

            var tempPassword = GenerateTemporaryPassword();
            var createResult = await _userManager.CreateAsync(maUser, tempPassword);

            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                TempData["Error"] = $"Failed to create MA account: {errors}";
                return RedirectToAction(nameof(ManageMarketingAccess), new { id });
            }

            await _userManager.AddToRoleAsync(maUser, "MarketingAgency");

            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "User",
                EntityId = 0,
                Action = "CreateMarketingAgencyUser",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = User.IsInRole("Admin") ? "Admin" : "Builder",
                NewValues = System.Text.Json.JsonSerializer.Serialize(new
                {
                    MAUserId = maUser.Id,
                    MAEmail = maUser.Email,
                    ProjectId = id
                }),
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            // Generate password reset link for invitation
            var token = await _userManager.GeneratePasswordResetTokenAsync(maUser);
            var resetLink = Url.Action("ResetPassword", "Account", new { area = "Identity", code = token }, Request.Scheme);

            TempData["Success"] = $"Marketing Agency user {maFirstName} {maLastName} created successfully. Select them from the dropdown below.";
            TempData["InvitationLink"] = resetLink;

            return RedirectToAction(nameof(ManageMarketingAccess), new { id });
        }

        // GET: /Projects/FeeReference
        // Ontario closing fee reference for builders — shows LTT rates and live flat fee schedule
        public async Task<IActionResult> FeeReference()
        {
            var fees = await _context.SystemFeeConfigs
                .OrderBy(f => f.Id)
                .ToListAsync();
            return View(fees);
        }

        // Helper method to generate temporary password
        private string GenerateTemporaryPassword()
        {
            // Generate a secure random password
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%";
            var random = new Random();
            var password = new string(Enumerable.Repeat(chars, 12)
                .Select(s => s[random.Next(s.Length)]).ToArray());

            // Ensure it meets password requirements
            return password + "Aa1!";
        }
        #region Export Excel

        // GET: /Projects/ExportExcel/5
        public async Task<IActionResult> ExportExcel(int id)
        {
            var userId = _userManager.GetUserId(User);

            var project = await _context.Projects
                .Include(p => p.Units)
                    .ThenInclude(u => u.Purchasers)
                        .ThenInclude(up => up.Purchaser)
                .Include(p => p.Units)
                    .ThenInclude(u => u.Purchasers)
                        .ThenInclude(up => up.MortgageInfo)
                .Include(p => p.Units)
                    .ThenInclude(u => u.Purchasers)
                        .ThenInclude(up => up.Financials)
                .Include(p => p.Units)
                    .ThenInclude(u => u.Deposits)
                .Include(p => p.Units)
                    .ThenInclude(u => u.SOA)
                .Include(p => p.Units)
                    .ThenInclude(u => u.ShortfallAnalysis)
                .Include(p => p.Units)
                    .ThenInclude(u => u.LawyerAssignments)
                        .ThenInclude(la => la.Lawyer)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            if (!User.IsInRole("Admin") && project.BuilderId != userId)
                return Forbid();

            using var workbook = new XLWorkbook();

            // ===== SUMMARY SHEET =====
            CreateSummarySheet(workbook, project);

            // ===== UNITS SHEET =====
            CreateUnitsSheet(workbook, project);

            // ===== DEPOSITS SHEET =====
            CreateDepositsSheet(workbook, project);

            // ===== PURCHASERS SHEET =====
            CreatePurchasersSheet(workbook, project);

            // Generate file
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();

            var fileName = $"{SanitizeFileName(project.Name)}_Export_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

            _logger.LogInformation("Excel export generated for Project {ProjectId} by user {UserId}", id, userId);

            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private void CreateSummarySheet(XLWorkbook workbook, Project project)
        {
            var sheet = workbook.Worksheets.Add("Summary");
            var units = project.Units.ToList();
            var totalUnits = units.Count;

            // Title
            sheet.Cell("A1").Value = "PROJECT CLOSING OVERVIEW";
            sheet.Cell("A1").Style.Font.Bold = true;
            sheet.Cell("A1").Style.Font.FontSize = 18;
            sheet.Cell("A1").Style.Font.FontColor = XLColor.DarkBlue;
            sheet.Range("A1:D1").Merge();

            // Project Info
            sheet.Cell("A3").Value = "Project Name:";
            sheet.Cell("A3").Style.Font.Bold = true;
            sheet.Cell("B3").Value = project.Name;

            sheet.Cell("A4").Value = "Address:";
            sheet.Cell("A4").Style.Font.Bold = true;
            sheet.Cell("B4").Value = $"{project.Address}, {project.City}, ON {project.PostalCode}";

            sheet.Cell("A5").Value = "Project Type:";
            sheet.Cell("A5").Style.Font.Bold = true;
            sheet.Cell("B5").Value = project.ProjectType.ToString();

            sheet.Cell("A6").Value = "Export Date:";
            sheet.Cell("A6").Style.Font.Bold = true;
            sheet.Cell("B6").Value = DateTime.Now.ToString("MMMM dd, yyyy h:mm tt");

            // Statistics Section
            sheet.Cell("A8").Value = "CLOSING STATISTICS";
            sheet.Cell("A8").Style.Font.Bold = true;
            sheet.Cell("A8").Style.Font.FontSize = 14;
            sheet.Cell("A8").Style.Fill.BackgroundColor = XLColor.LightBlue;
            sheet.Range("A8:D8").Merge();

            var statRow = 9;
            AddStatRow(sheet, ref statRow, "Total Units:", totalUnits.ToString());

            var readyToClose = units.Count(u => u.Recommendation == ClosingRecommendation.ProceedToClose);
            var pct = totalUnits > 0 ? (readyToClose * 100.0 / totalUnits).ToString("F1") + "%" : "0%";
            AddStatRow(sheet, ref statRow, "Ready to Close:", $"{readyToClose} ({pct})", XLColor.Green);

            var needsDiscount = units.Count(u => u.Recommendation == ClosingRecommendation.CloseWithDiscount);
            pct = totalUnits > 0 ? (needsDiscount * 100.0 / totalUnits).ToString("F1") + "%" : "0%";
            AddStatRow(sheet, ref statRow, "Closing with Discounts:", $"{needsDiscount} ({pct})", XLColor.Blue);

            var needsVTB = units.Count(u => u.Recommendation == ClosingRecommendation.VTBSecondMortgage ||
                                            u.Recommendation == ClosingRecommendation.VTBFirstMortgage);
            pct = totalUnits > 0 ? (needsVTB * 100.0 / totalUnits).ToString("F1") + "%" : "0%";
            AddStatRow(sheet, ref statRow, "Closing with VTB:", $"{needsVTB} ({pct})", XLColor.Orange);

            var atRisk = units.Count(u => u.Recommendation == ClosingRecommendation.HighRiskDefault ||
                                          u.Recommendation == ClosingRecommendation.PotentialDefault);
            pct = totalUnits > 0 ? (atRisk * 100.0 / totalUnits).ToString("F1") + "%" : "0%";
            AddStatRow(sheet, ref statRow, "At Risk of Default:", $"{atRisk} ({pct})", XLColor.Red);

            // Financial Summary
            statRow++;
            sheet.Cell(statRow, 1).Value = "FINANCIAL SUMMARY";
            sheet.Cell(statRow, 1).Style.Font.Bold = true;
            sheet.Cell(statRow, 1).Style.Font.FontSize = 14;
            sheet.Cell(statRow, 1).Style.Fill.BackgroundColor = XLColor.LightGreen;
            sheet.Range(statRow, 1, statRow, 4).Merge();
            statRow++;

            var totalSalesValue = units.Sum(u => u.PurchasePrice);
            AddFinancialRow(sheet, ref statRow, "Total Sales Value:", totalSalesValue);

            var totalDepositsCollected = units.SelectMany(u => u.Deposits).Where(d => d.IsPaid).Sum(d => d.Amount);
            AddFinancialRow(sheet, ref statRow, "Total Deposits Collected:", totalDepositsCollected);

            var totalDepositsPending = units.SelectMany(u => u.Deposits).Where(d => !d.IsPaid).Sum(d => d.Amount);
            AddFinancialRow(sheet, ref statRow, "Total Deposits Pending:", totalDepositsPending);

            var totalBalanceDue = units.Where(u => u.SOA != null).Sum(u => u.SOA!.BalanceDueOnClosing);
            AddFinancialRow(sheet, ref statRow, "Total Balance Due (all SOAs):", totalBalanceDue);

            var totalShortfall = units.Where(u => u.ShortfallAnalysis != null).Sum(u => u.ShortfallAnalysis!.ShortfallAmount);
            AddFinancialRow(sheet, ref statRow, "Total Shortfall:", totalShortfall);

            var totalDiscountRequired = units
                .Where(u => u.ShortfallAnalysis != null && u.Recommendation == ClosingRecommendation.CloseWithDiscount)
                .Sum(u => u.ShortfallAnalysis!.SuggestedDiscount ?? 0);
            AddFinancialRow(sheet, ref statRow, "Total Discounts Required:", totalDiscountRequired);

            sheet.Columns().AdjustToContents();
        }

        private void AddStatRow(IXLWorksheet sheet, ref int row, string label, string value, XLColor? valueColor = null)
        {
            sheet.Cell(row, 1).Value = label;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 2).Value = value;
            if (valueColor != null)
                sheet.Cell(row, 2).Style.Font.FontColor = valueColor;
            row++;
        }

        private void AddFinancialRow(IXLWorksheet sheet, ref int row, string label, decimal value)
        {
            sheet.Cell(row, 1).Value = label;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 2).Value = value;
            sheet.Cell(row, 2).Style.NumberFormat.Format = "$#,##0.00";
            row++;
        }

        private void CreateUnitsSheet(XLWorkbook workbook, Project project)
        {
            var sheet = workbook.Worksheets.Add("Units");
            var units = project.Units.OrderBy(u => u.UnitNumber).ToList();

            // Headers
            var headers = new[] {
        "Unit #", "Floor", "Type", "BR", "BA", "Sq Ft", "Purchase Price",
        "Parking", "Parking $", "Locker", "Locker $",
        "Occupancy Date", "Closing Date", "Status", "AI Recommendation",
        "Purchaser Name", "Purchaser Email", "Purchaser Phone",
        "Mortgage Approved", "Mortgage Amount", "Mortgage Provider",
        "Additional Cash", "Deposits Paid", "SOA Balance Due", "Cash Required",
        "Shortfall", "Shortfall %", "Lawyer Assigned", "Lawyer Confirmed"
    };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = sheet.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.DarkBlue;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Data rows
            int row = 2;
            foreach (var unit in units)
            {
                var primaryPurchaser = unit.Purchasers.FirstOrDefault(p => p.IsPrimaryPurchaser);
                var mortgageInfo = primaryPurchaser?.MortgageInfo;
                var financials = primaryPurchaser?.Financials;
                var lawyer = unit.LawyerAssignments.FirstOrDefault(la => la.IsActive);

                sheet.Cell(row, 1).Value = unit.UnitNumber;
                sheet.Cell(row, 2).Value = unit.FloorNumber ?? "";
                sheet.Cell(row, 3).Value = unit.UnitType.ToString();
                sheet.Cell(row, 4).Value = unit.Bedrooms;
                sheet.Cell(row, 5).Value = unit.Bathrooms;
                sheet.Cell(row, 6).Value = (double)unit.SquareFootage;

                sheet.Cell(row, 7).Value = (double)unit.PurchasePrice;
                sheet.Cell(row, 7).Style.NumberFormat.Format = "$#,##0";

                sheet.Cell(row, 8).Value = unit.HasParking ? "Yes" : "No";
                sheet.Cell(row, 9).Value = (double)unit.ParkingPrice;
                sheet.Cell(row, 9).Style.NumberFormat.Format = "$#,##0";

                sheet.Cell(row, 10).Value = unit.HasLocker ? "Yes" : "No";
                sheet.Cell(row, 11).Value = (double)unit.LockerPrice;
                sheet.Cell(row, 11).Style.NumberFormat.Format = "$#,##0";

                sheet.Cell(row, 12).Value = unit.OccupancyDate?.ToString("yyyy-MM-dd") ?? "";
                sheet.Cell(row, 13).Value = unit.ClosingDate?.ToString("yyyy-MM-dd") ?? "";
                sheet.Cell(row, 14).Value = unit.Status.ToString();

                // AI Recommendation with color coding
                var recCell = sheet.Cell(row, 15);
                recCell.Value = unit.Recommendation?.ToString() ?? "Pending";

                if (unit.Recommendation == ClosingRecommendation.ProceedToClose)
                    recCell.Style.Fill.BackgroundColor = XLColor.LightGreen;
                else if (unit.Recommendation == ClosingRecommendation.CloseWithDiscount)
                    recCell.Style.Fill.BackgroundColor = XLColor.LightBlue;
                else if (unit.Recommendation == ClosingRecommendation.VTBSecondMortgage ||
                         unit.Recommendation == ClosingRecommendation.VTBFirstMortgage)
                    recCell.Style.Fill.BackgroundColor = XLColor.LightYellow;
                else if (unit.Recommendation == ClosingRecommendation.HighRiskDefault ||
                         unit.Recommendation == ClosingRecommendation.PotentialDefault)
                    recCell.Style.Fill.BackgroundColor = XLColor.LightCoral;

                // Purchaser info
                if (primaryPurchaser != null)
                {
                    sheet.Cell(row, 16).Value = $"{primaryPurchaser.Purchaser.FirstName} {primaryPurchaser.Purchaser.LastName}";
                    sheet.Cell(row, 17).Value = primaryPurchaser.Purchaser.Email ?? "";
                    sheet.Cell(row, 18).Value = primaryPurchaser.Purchaser.PhoneNumber ?? "";
                }

                // Mortgage info
                sheet.Cell(row, 19).Value = mortgageInfo?.HasMortgageApproval == true ? "Yes" : "No";
                sheet.Cell(row, 20).Value = (double)(mortgageInfo?.ApprovedAmount ?? 0);
                sheet.Cell(row, 20).Style.NumberFormat.Format = "$#,##0";
                sheet.Cell(row, 21).Value = mortgageInfo?.MortgageProvider ?? "";

                // Financials
                sheet.Cell(row, 22).Value = (double)(financials?.AdditionalCashAvailable ?? 0);
                sheet.Cell(row, 22).Style.NumberFormat.Format = "$#,##0";

                // Deposits paid
                var depositsPaid = unit.Deposits.Where(d => d.IsPaid).Sum(d => d.Amount);
                sheet.Cell(row, 23).Value = (double)depositsPaid;
                sheet.Cell(row, 23).Style.NumberFormat.Format = "$#,##0";

                // SOA
                sheet.Cell(row, 24).Value = (double)(unit.SOA?.BalanceDueOnClosing ?? 0);
                sheet.Cell(row, 24).Style.NumberFormat.Format = "$#,##0";
                sheet.Cell(row, 25).Value = (double)(unit.SOA?.CashRequiredToClose ?? 0);
                sheet.Cell(row, 25).Style.NumberFormat.Format = "$#,##0";

                // Shortfall
                sheet.Cell(row, 26).Value = (double)(unit.ShortfallAnalysis?.ShortfallAmount ?? 0);
                sheet.Cell(row, 26).Style.NumberFormat.Format = "$#,##0";
                sheet.Cell(row, 27).Value = (double)(unit.ShortfallAnalysis?.ShortfallPercentage ?? 0) / 100;
                sheet.Cell(row, 27).Style.NumberFormat.Format = "0.00%";

                // Lawyer
                sheet.Cell(row, 28).Value = lawyer != null ? $"{lawyer.Lawyer.FirstName} {lawyer.Lawyer.LastName}" : "";
                sheet.Cell(row, 29).Value = unit.IsConfirmedByLawyer ? "Yes" : "No";

                row++;
            }

            // Auto-fit and freeze header
            sheet.Columns().AdjustToContents();
            sheet.SheetView.FreezeRows(1);
        }

        private void CreateDepositsSheet(XLWorkbook workbook, Project project)
        {
            var sheet = workbook.Worksheets.Add("Deposits");
            var units = project.Units.OrderBy(u => u.UnitNumber).ToList();

            // Headers
            var headers = new[] { "Unit #", "Purchaser", "Deposit Name", "Amount", "Due Date", "Status", "Paid Date" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = sheet.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.DarkBlue;
                cell.Style.Font.FontColor = XLColor.White;
            }

            int row = 2;
            foreach (var unit in units)
            {
                var purchaser = unit.Purchasers.FirstOrDefault(p => p.IsPrimaryPurchaser);
                var purchaserName = purchaser != null
                    ? $"{purchaser.Purchaser.FirstName} {purchaser.Purchaser.LastName}"
                    : "";

                foreach (var deposit in unit.Deposits.OrderBy(d => d.DueDate))
                {
                    sheet.Cell(row, 1).Value = unit.UnitNumber;
                    sheet.Cell(row, 2).Value = purchaserName;
                    sheet.Cell(row, 3).Value = deposit.DepositName;
                    sheet.Cell(row, 4).Value = (double)deposit.Amount;
                    sheet.Cell(row, 4).Style.NumberFormat.Format = "$#,##0";
                    sheet.Cell(row, 5).Value = deposit.DueDate.ToString("yyyy-MM-dd");

                    var statusCell = sheet.Cell(row, 6);
                    if (deposit.IsPaid)
                    {
                        statusCell.Value = "Paid";
                        statusCell.Style.Fill.BackgroundColor = XLColor.LightGreen;
                    }
                    else if (deposit.DueDate < DateTime.Now)
                    {
                        statusCell.Value = "Overdue";
                        statusCell.Style.Fill.BackgroundColor = XLColor.LightCoral;
                    }
                    else
                    {
                        statusCell.Value = "Pending";
                        statusCell.Style.Fill.BackgroundColor = XLColor.LightYellow;
                    }

                    sheet.Cell(row, 7).Value = deposit.PaidDate?.ToString("yyyy-MM-dd") ?? "";

                    row++;
                }
            }

            sheet.Columns().AdjustToContents();
            sheet.SheetView.FreezeRows(1);
        }

        private void CreatePurchasersSheet(XLWorkbook workbook, Project project)
        {
            var sheet = workbook.Worksheets.Add("Purchasers");
            var units = project.Units.OrderBy(u => u.UnitNumber).ToList();

            // Headers
            var headers = new[] {
        "Unit #", "Name", "Email", "Phone", "Primary", "Ownership %",
        "Mortgage Approved", "Mortgage Amount", "Provider", "Approval Type",
        "Additional Cash", "Annual Income"
    };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = sheet.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.DarkBlue;
                cell.Style.Font.FontColor = XLColor.White;
            }

            int row = 2;
            foreach (var unit in units)
            {
                foreach (var up in unit.Purchasers.OrderByDescending(p => p.IsPrimaryPurchaser))
                {
                    sheet.Cell(row, 1).Value = unit.UnitNumber;
                    sheet.Cell(row, 2).Value = $"{up.Purchaser.FirstName} {up.Purchaser.LastName}";
                    sheet.Cell(row, 3).Value = up.Purchaser.Email ?? "";
                    sheet.Cell(row, 4).Value = up.Purchaser.PhoneNumber ?? "";
                    sheet.Cell(row, 5).Value = up.IsPrimaryPurchaser ? "Yes" : "No";
                    sheet.Cell(row, 6).Value = (double)up.OwnershipPercentage;
                    sheet.Cell(row, 6).Style.NumberFormat.Format = "0%";

                    // Mortgage
                    sheet.Cell(row, 7).Value = up.MortgageInfo?.HasMortgageApproval == true ? "Yes" : "No";
                    sheet.Cell(row, 8).Value = (double)(up.MortgageInfo?.ApprovedAmount ?? 0);
                    sheet.Cell(row, 8).Style.NumberFormat.Format = "$#,##0";
                    sheet.Cell(row, 9).Value = up.MortgageInfo?.MortgageProvider ?? "";
                    sheet.Cell(row, 10).Value = up.MortgageInfo?.ApprovalType.ToString() ?? "";

                    // Financials
                    sheet.Cell(row, 11).Value = (double)(up.Financials?.AdditionalCashAvailable ?? 0);
                    sheet.Cell(row, 11).Style.NumberFormat.Format = "$#,##0";
                    sheet.Cell(row, 12).Value = (double)(up.Financials?.AnnualIncome ?? 0);
                    sheet.Cell(row, 12).Style.NumberFormat.Format = "$#,##0";

                    row++;
                }
            }

            sheet.Columns().AdjustToContents();
            sheet.SheetView.FreezeRows(1);
        }

        private string SanitizeFileName(string fileName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        }

        #endregion

        #region Project Investment Management

        // GET: /Projects/ProjectInvestment/5
        // Builder-only view to manage project-level financial data used by AI allocation engine
        public async Task<IActionResult> ProjectInvestment(int id)
        {
            var userId = _userManager.GetUserId(User);
            var project = await _context.Projects
                .Include(p => p.Financials)
                .Include(p => p.Units)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null) return NotFound();
            if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin") && project.BuilderId != userId)
                return Forbid();

            var financials = project.Financials;
            var totalUnits = project.Units.Count;
            var unsoldUnits = project.Units.Count(u => u.Status != UnitStatus.Closed && u.Status != UnitStatus.Defaulted && u.Status != UnitStatus.Cancelled);

            var vm = new ProjectInvestmentViewModel
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                TotalRevenue = financials?.TotalRevenue ?? 0,
                TotalInvestment = financials?.TotalInvestment ?? 0,
                MarketingCost = financials?.MarketingCost ?? 0,
                ProfitAvailable = financials?.ProfitAvailable ?? 0,
                MaxBuilderCapital = financials?.MaxBuilderCapital ?? 0,
                Notes = financials?.Notes,
                TotalUnits = totalUnits,
                UnsoldUnits = unsoldUnits
            };

            return View(vm);
        }

        // POST: /Projects/ProjectInvestment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProjectInvestment(int id, ProjectInvestmentViewModel model)
        {
            var userId = _userManager.GetUserId(User);
            var project = await _context.Projects
                .Include(p => p.Financials)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null) return NotFound();
            if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin") && project.BuilderId != userId)
                return Forbid();

            if (!ModelState.IsValid)
            {
                model.ProjectId = id;
                model.ProjectName = project.Name;
                return View(model);
            }

            var financials = project.Financials;
            if (financials == null)
            {
                financials = new ProjectFinancials
                {
                    ProjectId = id,
                    CreatedAt = DateTime.UtcNow
                };
                _context.ProjectFinancials.Add(financials);
            }

            financials.TotalRevenue = model.TotalRevenue;
            financials.TotalInvestment = model.TotalInvestment;
            financials.MarketingCost = model.MarketingCost;
            // ProfitAvailable is auto-calculated, ignore submitted value
            financials.ProfitAvailable = model.TotalRevenue - model.TotalInvestment - model.MarketingCost;
            financials.MaxBuilderCapital = model.MaxBuilderCapital;
            financials.Notes = model.Notes;
            financials.UpdatedAt = DateTime.UtcNow;
            financials.UpdatedByUserId = userId;

            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "ProjectFinancials",
                EntityId = id,
                Action = "UpdateProjectInvestment",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = User.IsInRole("Admin") ? "Admin" : "Builder",
                NewValues = System.Text.Json.JsonSerializer.Serialize(new
                {
                    model.TotalRevenue,
                    model.TotalInvestment,
                    model.MarketingCost,
                    model.ProfitAvailable,
                    model.MaxBuilderCapital
                }),
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = "Project investment data updated successfully.";
            return RedirectToAction(nameof(Dashboard), new { id });
        }

        #endregion

        #region Builder User Management (MyUsers)

        // GET: /Projects/MyUsers
        [Authorize(Roles = "Builder")]
        public async Task<IActionResult> MyUsers()
        {
            var userId = _userManager.GetUserId(User);

            var invitedUsers = await _context.Users
                .Where(u => u.CreatedByUserId == userId)
                .OrderByDescending(u => u.CreatedAt)
                .Select(u => new BuilderInvitedUserItem
                {
                    UserId = u.Id,
                    FullName = u.FirstName + " " + u.LastName,
                    Email = u.Email ?? "",
                    UserType = u.UserType,
                    IsActive = u.IsActive,
                    EmailConfirmed = u.EmailConfirmed,
                    CreatedAt = u.CreatedAt,
                    LastLoginAt = u.LastLoginAt,
                    AssignedUnits = u.UserType == UserType.Purchaser
                        ? u.PurchaserUnits.Count
                        : u.UserType == UserType.Lawyer
                            ? u.LawyerAssignments.Count
                            : 0
                })
                .ToListAsync();

            var viewModel = new BuilderMyUsersViewModel
            {
                Users = invitedUsers,
                TotalCount = invitedUsers.Count
            };

            return View(viewModel);
        }

        // POST: /Projects/ToggleInvitedUserStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Builder")]
        public async Task<IActionResult> ToggleInvitedUserStatus(string targetUserId)
        {
            var userId = _userManager.GetUserId(User);
            var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId && u.CreatedByUserId == userId);

            if (targetUser == null)
            {
                TempData["Error"] = "User not found or you don't have permission.";
                return RedirectToAction(nameof(MyUsers));
            }

            targetUser.IsActive = !targetUser.IsActive;
            await _context.SaveChangesAsync();

            var status = targetUser.IsActive ? "activated" : "deactivated";
            TempData["Success"] = $"User {targetUser.Email} has been {status}.";
            return RedirectToAction(nameof(MyUsers));
        }

        // POST: /Projects/ResendUserInvitation
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Builder")]
        public async Task<IActionResult> ResendUserInvitation(string targetUserId)
        {
            var userId = _userManager.GetUserId(User);
            var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId && u.CreatedByUserId == userId);

            if (targetUser == null)
            {
                TempData["Error"] = "User not found or you don't have permission.";
                return RedirectToAction(nameof(MyUsers));
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(targetUser);
            var resetLink = Url.Action("ResetPassword", "Account", new { area = "Identity", code = token }, Request.Scheme);

            TempData["Success"] = $"Invitation link for {targetUser.Email}: {resetLink}";
            return RedirectToAction(nameof(MyUsers));
        }

        #endregion

    }
}
