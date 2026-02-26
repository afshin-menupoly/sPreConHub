using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PreConHub.Data;
using PreConHub.Models.Entities;
using PreConHub.Models.ViewModels;
using PreConHub.Services;
using System.Text.Json;

namespace PreConHub.Controllers
{
    [Authorize(Roles = "Lawyer")]
    public class BuyerLawyerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<BuyerLawyerController> _logger;
        private readonly IEmailService _emailService;
        private readonly IPdfService _pdfService;
        private readonly INotificationService _notificationService;
        private readonly ISoaCalculationService _soaService;
        private readonly IShortfallAnalysisService _shortfallService;
        private readonly IWebHostEnvironment _environment;

        public BuyerLawyerController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailService emailService,
            IPdfService pdfService,
            INotificationService notificationService,
            ISoaCalculationService soaService,
            IShortfallAnalysisService shortfallService,
            IWebHostEnvironment environment,
            ILogger<BuyerLawyerController> logger)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _pdfService = pdfService;
            _notificationService = notificationService;
            _soaService = soaService;
            _shortfallService = shortfallService;
            _environment = environment;
            _logger = logger;
        }

        // GET: /BuyerLawyer/Dashboard
        public async Task<IActionResult> Dashboard(
            int? projectFilter = null, string? search = null, int page = 1, int pageSize = 25)
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.GetUserAsync(User);

            // Validate inputs
            if (!new[] { 25, 50, 100, 250, 500 }.Contains(pageSize)) pageSize = 25;
            if (page < 1) page = 1;

            // Get all units assigned to this lawyer as PurchaserLawyer
            var assignments = await _context.LawyerAssignments
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Project)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Purchasers)
                        .ThenInclude(up => up.Purchaser)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Purchasers)
                        .ThenInclude(up => up.MortgageInfo)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Purchasers)
                        .ThenInclude(up => up.Financials)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.SOA)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.ShortfallAnalysis)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Documents)
                .Where(la => la.LawyerId == userId
                          && la.Role == LawyerRole.PurchaserLawyer
                          && la.IsActive)
                .ToListAsync();

            // Map to view models
            var allUnits = assignments.Select(la =>
            {
                var primaryPurchaser = la.Unit.Purchasers.FirstOrDefault(p => p.IsPrimaryPurchaser);
                return new BuyerLawyerUnitViewModel
                {
                    AssignmentId = la.Id,
                    UnitId = la.Unit.Id,
                    UnitNumber = la.Unit.UnitNumber,
                    ProjectName = la.Unit.Project.Name,

                    PurchaserName = primaryPurchaser != null
                        ? $"{primaryPurchaser.Purchaser.FirstName} {primaryPurchaser.Purchaser.LastName}"
                        : "Not Assigned",
                    PurchaserEmail = primaryPurchaser?.Purchaser.Email,
                    PurchaserPhone = primaryPurchaser?.Purchaser.PhoneNumber,

                    ClosingDate = la.Unit.ClosingDate ?? la.Unit.Project.ClosingDate,
                    PurchasePrice = la.Unit.PurchasePrice,

                    MortgageAmount = primaryPurchaser?.MortgageInfo?.ApprovedAmount ?? 0m,
                    CashRequiredToClose = la.Unit.SOA?.CashRequiredToClose ?? 0m,

                    ReviewStatus = la.ReviewStatus,
                    AssignedAt = la.AssignedAt,

                    HasSOA = la.Unit.SOA != null,
                    HasMortgageInfo = primaryPurchaser?.MortgageInfo != null
                                      && primaryPurchaser.MortgageInfo.HasMortgageApproval,
                    HasFinancials = primaryPurchaser?.Financials != null,
                    DocumentCount = la.Unit.Documents.Count
                };
            }).ToList();

            // Build project dropdown from lawyer's assigned projects
            var projects = allUnits
                .Select(u => new LawyerProjectFilterItem
                {
                    Id = allUnits.First(x => x.ProjectName == u.ProjectName).UnitId, // need ProjectId
                    Name = u.ProjectName
                })
                .DistinctBy(p => p.Name)
                .OrderBy(p => p.Name)
                .ToList();

            // We need actual ProjectIds - re-derive from assignments
            var projectItems = assignments
                .Select(la => new LawyerProjectFilterItem
                {
                    Id = la.Unit.Project.Id,
                    Name = la.Unit.Project.Name
                })
                .DistinctBy(p => p.Id)
                .OrderBy(p => p.Name)
                .ToList();

            // Summary stats (computed from ALL units, before filtering)
            var totalAssigned = allUnits.Count;
            var pendingReview = allUnits.Count(u => u.ReviewStatus == LawyerReviewStatus.Pending);
            var underReview = allUnits.Count(u => u.ReviewStatus == LawyerReviewStatus.UnderReview);
            var approved = allUnits.Count(u => u.ReviewStatus == LawyerReviewStatus.Approved);
            var needsAttention = allUnits.Count(u => u.ReviewStatus == LawyerReviewStatus.NeedsRevision);

            // Apply filters
            IEnumerable<BuyerLawyerUnitViewModel> filtered = allUnits;

            // Project filter - need to map projectFilter to project name since BuyerLawyerUnitViewModel
            // doesn't have ProjectId. Use assignments to find matching units.
            if (projectFilter.HasValue)
            {
                var unitIdsForProject = assignments
                    .Where(la => la.Unit.ProjectId == projectFilter.Value)
                    .Select(la => la.Unit.Id)
                    .ToHashSet();
                filtered = filtered.Where(u => unitIdsForProject.Contains(u.UnitId));
            }

            // Search filter (unit number starts-with OR purchaser name contains)
            if (!string.IsNullOrWhiteSpace(search) && search.Trim().Length >= 1)
            {
                var term = search.Trim();
                filtered = filtered.Where(u =>
                    u.UnitNumber.StartsWith(term, StringComparison.OrdinalIgnoreCase) ||
                    u.PurchaserName.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            // Sort by closing date
            var filteredList = filtered.OrderBy(u => u.ClosingDate).ToList();

            // Pagination
            var totalFiltered = filteredList.Count;
            var totalPages = (int)Math.Ceiling(totalFiltered / (double)pageSize);
            if (page > totalPages && totalPages > 0) page = totalPages;

            var pagedUnits = filteredList
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var viewModel = new BuyerLawyerDashboardViewModel
            {
                LawyerName = $"{user?.FirstName} {user?.LastName}",
                LawyerFirm = user?.CompanyName ?? user?.LawFirm,
                Units = pagedUnits,
                TotalAssigned = totalAssigned,
                PendingReview = pendingReview,
                UnderReview = underReview,
                Approved = approved,
                NeedsAttention = needsAttention,
                CurrentPage = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                TotalFilteredUnits = totalFiltered,
                ProjectFilter = projectFilter,
                SearchQuery = search,
                Projects = projectItems
            };

            return View(viewModel);
        }

        // GET: /BuyerLawyer/ReviewUnit/5 (AssignmentId)
        public async Task<IActionResult> ReviewUnit(int id)
        {
            var userId = _userManager.GetUserId(User);

            var assignment = await _context.LawyerAssignments
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Project)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Purchasers)
                        .ThenInclude(up => up.Purchaser)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Purchasers)
                        .ThenInclude(up => up.MortgageInfo)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Purchasers)
                        .ThenInclude(up => up.Financials)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.SOA)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.ShortfallAnalysis)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Deposits)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Documents)
                        .ThenInclude(d => d.UploadedBy)
                .Include(la => la.LawyerNotes)
                .FirstOrDefaultAsync(la => la.Id == id
                                        && la.LawyerId == userId
                                        && la.Role == LawyerRole.PurchaserLawyer);

            if (assignment == null)
                return NotFound();

            var unit = assignment.Unit;
            var primaryPurchaser = unit.Purchasers.FirstOrDefault(p => p.IsPrimaryPurchaser);

            // Load builder's lawyer assignment for coordination info
            var builderLawyerAssignment = await _context.LawyerAssignments
                .Include(la => la.Lawyer)
                .Where(la => la.UnitId == unit.Id
                          && la.Role == LawyerRole.BuilderLawyer
                          && la.IsActive)
                .FirstOrDefaultAsync();

            var viewModel = new BuyerLawyerReviewViewModel
            {
                AssignmentId = assignment.Id,
                UnitId = unit.Id,

                // Project Info
                ProjectName = unit.Project.Name,
                ProjectAddress = $"{unit.Project.Address}, {unit.Project.City}, ON {unit.Project.PostalCode}",

                // Unit Info
                UnitNumber = unit.UnitNumber,
                UnitType = unit.UnitType,
                Bedrooms = unit.Bedrooms,
                Bathrooms = unit.Bathrooms,
                SquareFootage = (int)unit.SquareFootage,
                PurchasePrice = unit.PurchasePrice,
                OccupancyDate = unit.OccupancyDate,
                ClosingDate = unit.ClosingDate ?? unit.Project.ClosingDate,

                // Purchaser Info
                PurchaserName = primaryPurchaser != null
                    ? $"{primaryPurchaser.Purchaser.FirstName} {primaryPurchaser.Purchaser.LastName}"
                    : "Not Assigned",
                PurchaserEmail = primaryPurchaser?.Purchaser.Email,
                PurchaserPhone = primaryPurchaser?.Purchaser.PhoneNumber,

                // Co-Purchasers
                CoPurchasers = unit.Purchasers
                    .Where(p => !p.IsPrimaryPurchaser)
                    .Select(p => $"{p.Purchaser.FirstName} {p.Purchaser.LastName}")
                    .ToList(),

                // Mortgage Info (detailed)
                HasMortgageApproval = primaryPurchaser?.MortgageInfo?.HasMortgageApproval ?? false,
                MortgageProvider = primaryPurchaser?.MortgageInfo?.MortgageProvider,
                MortgageAmount = primaryPurchaser?.MortgageInfo?.ApprovedAmount ?? 0,
                MortgageApprovalType = primaryPurchaser?.MortgageInfo?.ApprovalType ?? MortgageApprovalType.None,
                MortgageExpiryDate = primaryPurchaser?.MortgageInfo?.ApprovalExpiryDate,
                MortgageConditions = primaryPurchaser?.MortgageInfo?.Conditions,
                CreditScore = primaryPurchaser?.MortgageInfo?.CreditScore,
                InterestRate = primaryPurchaser?.MortgageInfo?.InterestRate,
                AmortizationYears = primaryPurchaser?.MortgageInfo?.AmortizationYears,

                // Financial Info (detailed)
                AdditionalCashAvailable = primaryPurchaser?.Financials?.AdditionalCashAvailable ?? 0,
                TotalFundsAvailable = primaryPurchaser?.Financials?.TotalFundsAvailable ?? 0,
                RRSPAvailable = primaryPurchaser?.Financials?.RRSPAvailable ?? 0,
                GiftFromFamily = primaryPurchaser?.Financials?.GiftFromFamily ?? 0,
                ProceedsFromSale = primaryPurchaser?.Financials?.ProceedsFromSale ?? 0,
                OtherFundsAmount = primaryPurchaser?.Financials?.OtherFundsAmount ?? 0,
                OtherFundsDescription = primaryPurchaser?.Financials?.OtherFundsDescription,
                HasExistingPropertyToSell = primaryPurchaser?.Financials?.HasExistingPropertyToSell ?? false,
                ExistingPropertyValue = primaryPurchaser?.Financials?.ExistingPropertyValue ?? 0,
                ExistingMortgageBalance = primaryPurchaser?.Financials?.ExistingMortgageBalance ?? 0,

                // Deposits
                Deposits = unit.Deposits.Select(d => new DepositViewModel
                {
                    Id = d.Id,
                    DepositName = d.DepositName,
                    Amount = d.Amount,
                    DueDate = d.DueDate,
                    PaidDate = d.PaidDate,
                    IsPaid = d.IsPaid,
                    Status = d.Status,
                    Holder = d.Holder.ToString()
                }).OrderBy(d => d.DueDate).ToList(),
                TotalDepositsPaid = unit.Deposits.Where(d => d.IsPaid).Sum(d => d.Amount),

                // SOA
                HasSOA = unit.SOA != null,
                SOA = unit.SOA,

                // Shortfall
                ShortfallAmount = unit.ShortfallAnalysis?.ShortfallAmount ?? 0,
                ShortfallPercentage = unit.ShortfallAnalysis?.ShortfallPercentage ?? 0,
                Recommendation = unit.Recommendation,

                // Review Status
                ReviewStatus = assignment.ReviewStatus,
                AssignedAt = assignment.AssignedAt,
                ReviewedAt = assignment.ReviewedAt,

                // Existing Notes
                Notes = assignment.LawyerNotes?
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n => new LawyerNoteViewModel
                    {
                        NoteId = n.Id,
                        Note = n.Note,
                        NoteType = n.NoteType,
                        Visibility = (int)n.Visibility,
                        CreatedAt = n.CreatedAt
                    }).ToList() ?? new List<LawyerNoteViewModel>(),

                // Documents — split by source
                PurchaserDocuments = unit.Documents
                    .Where(d => d.Source == DocumentSource.Purchaser)
                    .Select(d => new DocumentViewModel
                    {
                        Id = d.Id,
                        FileName = d.FileName,
                        DocumentType = d.DocumentType,
                        DocumentTypeName = d.DocumentType.ToString(),
                        Description = d.Description,
                        FileSize = d.FileSize,
                        UploadedAt = d.UploadedAt
                    }).OrderByDescending(d => d.UploadedAt).ToList(),

                BuyerLawyerDocuments = unit.Documents
                    .Where(d => d.Source == DocumentSource.Lawyer && d.UploadedById == userId)
                    .Select(d => new DocumentViewModel
                    {
                        Id = d.Id,
                        FileName = d.FileName,
                        DocumentType = d.DocumentType,
                        DocumentTypeName = d.DocumentType.ToString(),
                        Description = d.Description,
                        FileSize = d.FileSize,
                        UploadedAt = d.UploadedAt
                    }).OrderByDescending(d => d.UploadedAt).ToList(),

                // Builder's lawyer info (for coordination)
                BuilderLawyerName = builderLawyerAssignment != null
                    ? $"{builderLawyerAssignment.Lawyer.FirstName} {builderLawyerAssignment.Lawyer.LastName}"
                    : null,
                BuilderLawyerFirm = builderLawyerAssignment?.Lawyer.CompanyName
                                    ?? builderLawyerAssignment?.Lawyer.LawFirm,
                BuilderLawyerReviewStatus = builderLawyerAssignment?.ReviewStatus
            };

            return View(viewModel);
        }

        // POST: /BuyerLawyer/StartReview/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartReview(int id)
        {
            var userId = _userManager.GetUserId(User);
            var assignment = await _context.LawyerAssignments
                .Include(la => la.Unit)
                .FirstOrDefaultAsync(la => la.Id == id
                                        && la.LawyerId == userId
                                        && la.Role == LawyerRole.PurchaserLawyer);

            if (assignment == null)
                return NotFound();

            // Builder Decision lock check
            if (assignment.Unit != null
                && assignment.Unit.BuilderDecision.HasValue
                && assignment.Unit.BuilderDecision != BuilderDecision.None)
            {
                TempData["Error"] = "This unit is locked — the builder has made a closing decision.";
                return RedirectToAction(nameof(ReviewUnit), new { id });
            }

            assignment.ReviewStatus = LawyerReviewStatus.UnderReview;
            assignment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Review started. You can now add notes and complete your review.";
            return RedirectToAction(nameof(ReviewUnit), new { id });
        }

        // POST: /BuyerLawyer/ApproveBuyerInfo/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveBuyerInfo(int id, string? approvalNotes)
        {
            var userId = _userManager.GetUserId(User);
            var assignment = await _context.LawyerAssignments
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Project)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.SOA)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Purchasers)
                        .ThenInclude(up => up.Purchaser)
                .FirstOrDefaultAsync(la => la.Id == id
                                        && la.LawyerId == userId
                                        && la.Role == LawyerRole.PurchaserLawyer);

            if (assignment == null)
                return NotFound();

            var unit = assignment.Unit;

            // Builder Decision lock check
            if (unit.BuilderDecision.HasValue && unit.BuilderDecision != BuilderDecision.None)
            {
                TempData["Error"] = "This unit is locked — the builder has made a closing decision.";
                return RedirectToAction(nameof(ReviewUnit), new { id });
            }

            // Update assignment status
            assignment.ReviewStatus = LawyerReviewStatus.Approved;
            assignment.ReviewedAt = DateTime.UtcNow;
            assignment.UpdatedAt = DateTime.UtcNow;

            // Update unit buyer-lawyer confirmation
            unit.BuyerLawyerConfirmed = true;
            unit.BuyerLawyerConfirmedAt = DateTime.UtcNow;
            unit.UpdatedAt = DateTime.UtcNow;

            // Update SOA buyer-lawyer confirmation
            if (unit.SOA != null)
            {
                unit.SOA.IsConfirmedByBuyerLawyer = true;
                unit.SOA.BuyerLawyerConfirmedAt = DateTime.UtcNow;
                unit.SOA.ConfirmedByBuyerLawyerId = userId;

                if (!string.IsNullOrWhiteSpace(approvalNotes))
                {
                    unit.SOA.BuyerLawyerNotes = approvalNotes;
                }
            }

            // Add approval note if provided
            if (!string.IsNullOrWhiteSpace(approvalNotes))
            {
                var note = new LawyerNote
                {
                    LawyerAssignmentId = id,
                    Note = approvalNotes,
                    NoteType = LawyerNoteType.Approval,
                    Visibility = NoteVisibility.ForPurchaser,
                    CreatedAt = DateTime.UtcNow
                };
                _context.LawyerNotes.Add(note);
            }

            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "LawyerAssignment",
                EntityId = assignment.UnitId ?? 0,
                Action = "BuyerLawyerApprove",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = "Lawyer",
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            // Notify the primary purchaser
            var primaryPurchaser = unit.Purchasers.FirstOrDefault(p => p.IsPrimaryPurchaser);
            if (primaryPurchaser != null)
            {
                var lawyer = await _userManager.GetUserAsync(User);
                var lawyerName = $"{lawyer?.FirstName} {lawyer?.LastName}".Trim();

                await _notificationService.CreateAsync(
                    userId: primaryPurchaser.PurchaserId,
                    title: "Buyer's Lawyer Approved",
                    message: $"{lawyerName} has reviewed and approved your information for Unit {unit.UnitNumber} in {unit.Project.Name}.",
                    type: NotificationType.Lawyer,
                    actionUrl: "/Purchaser/Dashboard",
                    unitId: unit.Id
                );
            }

            _logger.LogInformation("Buyer's lawyer {UserId} approved buyer info for Unit {UnitId}",
                userId, assignment.UnitId);

            TempData["Success"] = $"Unit {unit.UnitNumber} buyer information has been approved.";
            return RedirectToAction(nameof(ReviewUnit), new { id });
        }

        // POST: /BuyerLawyer/RequestRevision/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestRevision(int id, string revisionNotes)
        {
            var userId = _userManager.GetUserId(User);
            var assignment = await _context.LawyerAssignments
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Project)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Purchasers)
                        .ThenInclude(up => up.Purchaser)
                .FirstOrDefaultAsync(la => la.Id == id
                                        && la.LawyerId == userId
                                        && la.Role == LawyerRole.PurchaserLawyer);

            if (assignment == null)
                return NotFound();

            var unit = assignment.Unit;

            // Builder Decision lock check
            if (unit.BuilderDecision.HasValue && unit.BuilderDecision != BuilderDecision.None)
            {
                TempData["Error"] = "This unit is locked — the builder has made a closing decision.";
                return RedirectToAction(nameof(ReviewUnit), new { id });
            }

            if (string.IsNullOrWhiteSpace(revisionNotes))
            {
                TempData["Error"] = "Please provide details about what needs to be revised.";
                return RedirectToAction(nameof(ReviewUnit), new { id });
            }

            assignment.ReviewStatus = LawyerReviewStatus.NeedsRevision;
            assignment.UpdatedAt = DateTime.UtcNow;

            // Add revision note with ForPurchaser visibility
            var note = new LawyerNote
            {
                LawyerAssignmentId = id,
                Note = revisionNotes,
                NoteType = LawyerNoteType.RevisionRequest,
                Visibility = NoteVisibility.ForPurchaser,
                CreatedAt = DateTime.UtcNow
            };
            _context.LawyerNotes.Add(note);

            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "LawyerAssignment",
                EntityId = assignment.UnitId ?? 0,
                Action = "BuyerLawyerRequestRevision",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = "Lawyer",
                NewValues = JsonSerializer.Serialize(new { revisionNotes }),
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            // Notify the primary purchaser
            var primaryPurchaser = unit.Purchasers.FirstOrDefault(p => p.IsPrimaryPurchaser);
            if (primaryPurchaser != null)
            {
                var lawyer = await _userManager.GetUserAsync(User);
                var lawyerName = $"{lawyer?.FirstName} {lawyer?.LastName}".Trim();
                var truncatedNotes = revisionNotes.Length > 100
                    ? revisionNotes.Substring(0, 100) + "..."
                    : revisionNotes;

                await _notificationService.CreateAsync(
                    userId: primaryPurchaser.PurchaserId,
                    title: "Revision Requested by Your Lawyer",
                    message: $"{lawyerName} has requested revisions for Unit {unit.UnitNumber}: {truncatedNotes}",
                    type: NotificationType.Lawyer,
                    priority: NotificationPriority.High,
                    actionUrl: "/Purchaser/Dashboard",
                    unitId: unit.Id
                );
            }

            _logger.LogInformation("Buyer's lawyer {UserId} requested revision for Unit {UnitId}",
                userId, assignment.UnitId);

            TempData["Success"] = $"Revision requested for Unit {unit.UnitNumber}. The purchaser will be notified.";
            return RedirectToAction(nameof(ReviewUnit), new { id });
        }

        // POST: /BuyerLawyer/AddNote
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNote(int assignmentId, string note, int noteType, int visibility)
        {
            var userId = _userManager.GetUserId(User);

            var assignment = await _context.LawyerAssignments
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Project)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Purchasers)
                        .ThenInclude(up => up.Purchaser)
                .FirstOrDefaultAsync(la => la.Id == assignmentId
                                        && la.Role == LawyerRole.PurchaserLawyer);

            if (assignment == null)
                return NotFound();

            if (assignment.LawyerId != userId)
                return Forbid();

            // Builder Decision lock check
            if (assignment.Unit != null
                && assignment.Unit.BuilderDecision.HasValue
                && assignment.Unit.BuilderDecision != BuilderDecision.None)
            {
                TempData["Error"] = "This unit is locked — the builder has made a closing decision.";
                return RedirectToAction(nameof(ReviewUnit), new { id = assignmentId });
            }

            if (string.IsNullOrWhiteSpace(note))
            {
                TempData["Error"] = "Note content cannot be empty.";
                return RedirectToAction(nameof(ReviewUnit), new { id = assignmentId });
            }

            // Validate and cast enums
            var parsedNoteType = (LawyerNoteType)noteType;
            var parsedVisibility = (NoteVisibility)visibility;

            // Buyer's lawyer visibility options: Internal(0), ForPurchaser(3), Collaborative(2)
            if (parsedVisibility != NoteVisibility.Internal
                && parsedVisibility != NoteVisibility.ForPurchaser
                && parsedVisibility != NoteVisibility.Collaborative)
            {
                parsedVisibility = NoteVisibility.Internal;
            }

            var lawyerNote = new LawyerNote
            {
                LawyerAssignmentId = assignmentId,
                Note = note,
                NoteType = parsedNoteType,
                Visibility = parsedVisibility,
                CreatedAt = DateTime.UtcNow,
                IsReadByBuilder = false
            };

            _context.Add(lawyerNote);

            // Update assignment status if needed
            if (assignment.ReviewStatus == LawyerReviewStatus.Pending)
            {
                assignment.ReviewStatus = LawyerReviewStatus.UnderReview;
            }
            assignment.UpdatedAt = DateTime.UtcNow;

            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "LawyerNote",
                EntityId = assignment.UnitId ?? 0,
                Action = "BuyerLawyerAddNote",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = "Lawyer",
                NewValues = JsonSerializer.Serialize(new { noteType, visibility }),
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            // If note is ForPurchaser, notify the purchaser
            if (parsedVisibility == NoteVisibility.ForPurchaser)
            {
                var primaryPurchaser = assignment.Unit.Purchasers
                    .FirstOrDefault(p => p.IsPrimaryPurchaser);
                if (primaryPurchaser != null)
                {
                    var lawyer = await _userManager.GetUserAsync(User);
                    var lawyerName = $"{lawyer?.FirstName} {lawyer?.LastName}".Trim();

                    await _notificationService.CreateAsync(
                        userId: primaryPurchaser.PurchaserId,
                        title: "Note from Your Lawyer",
                        message: $"{lawyerName} added a note for Unit {assignment.Unit.UnitNumber} in {assignment.Unit.Project.Name}.",
                        type: NotificationType.Lawyer,
                        actionUrl: "/Purchaser/Dashboard",
                        unitId: assignment.Unit.Id
                    );
                }
            }

            _logger.LogInformation("Buyer's lawyer {LawyerId} added {NoteType} note to assignment {AssignmentId}",
                userId, parsedNoteType, assignmentId);

            TempData["Success"] = "Note added successfully.";
            return RedirectToAction(nameof(ReviewUnit), new { id = assignmentId });
        }

        // POST: /BuyerLawyer/UploadDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadDocument(int assignmentId, int unitId, IFormFile file,
            int documentType, string? description)
        {
            var userId = _userManager.GetUserId(User);

            var assignment = await _context.LawyerAssignments
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Project)
                .FirstOrDefaultAsync(la => la.Id == assignmentId
                                        && la.LawyerId == userId
                                        && la.Role == LawyerRole.PurchaserLawyer);

            if (assignment == null)
                return NotFound();

            // Builder Decision lock check
            if (assignment.Unit.BuilderDecision.HasValue
                && assignment.Unit.BuilderDecision != BuilderDecision.None)
            {
                TempData["Error"] = "This unit is locked — the builder has made a closing decision.";
                return RedirectToAction(nameof(ReviewUnit), new { id = assignmentId });
            }

            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                return RedirectToAction(nameof(ReviewUnit), new { id = assignmentId });
            }

            // Validate file size (max 10 MB)
            if (file.Length > 10 * 1024 * 1024)
            {
                TempData["Error"] = "File size cannot exceed 10 MB.";
                return RedirectToAction(nameof(ReviewUnit), new { id = assignmentId });
            }

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png", ".xlsx", ".xls" };
            if (!allowedExtensions.Contains(ext))
            {
                TempData["Error"] = "File type not supported. Please upload PDF, DOC, DOCX, JPG, PNG, or Excel files.";
                return RedirectToAction(nameof(ReviewUnit), new { id = assignmentId });
            }

            var unit = assignment.Unit;

            // Save file to wwwroot/uploads/documents/{unitId}/
            var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads", "documents", unit.Id.ToString());
            Directory.CreateDirectory(uploadsDir);

            var savedFileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsDir, savedFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Create Document entity
            var document = new Document
            {
                UnitId = unit.Id,
                FileName = file.FileName,
                FilePath = $"/uploads/documents/{unit.Id}/{savedFileName}",
                FileSize = file.Length,
                ContentType = file.ContentType,
                DocumentType = (DocumentType)documentType,
                Source = DocumentSource.Lawyer,
                Description = description,
                UploadedAt = DateTime.UtcNow,
                UploadedById = userId!
            };
            _context.Documents.Add(document);

            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "Document",
                EntityId = unit.Id,
                Action = "BuyerLawyerUpload",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = "Lawyer",
                NewValues = JsonSerializer.Serialize(new
                {
                    FileName = file.FileName,
                    DocumentType = ((DocumentType)documentType).ToString(),
                    FileSize = file.Length
                }),
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("Buyer's lawyer {UserId} uploaded document for Unit {UnitId}: {FileName}",
                userId, unit.Id, file.FileName);

            TempData["Success"] = "Document uploaded successfully.";
            return RedirectToAction(nameof(ReviewUnit), new { id = assignmentId });
        }

        // POST: /BuyerLawyer/DeleteDocument/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDocument(int id)
        {
            var userId = _userManager.GetUserId(User);

            var document = await _context.Documents
                .Include(d => d.Unit)
                    .ThenInclude(u => u!.LawyerAssignments)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
                return NotFound();

            // Verify document was uploaded by this lawyer
            if (document.UploadedById != userId)
            {
                TempData["Error"] = "You can only delete documents you have uploaded.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Verify this lawyer has a PurchaserLawyer assignment on the document's unit
            var hasAssignment = document.Unit?.LawyerAssignments
                .Any(la => la.LawyerId == userId
                         && la.Role == LawyerRole.PurchaserLawyer
                         && la.IsActive) ?? false;

            if (!hasAssignment)
                return Forbid();

            // Find the assignment for redirect
            var assignment = document.Unit?.LawyerAssignments
                .FirstOrDefault(la => la.LawyerId == userId
                                   && la.Role == LawyerRole.PurchaserLawyer
                                   && la.IsActive);

            // Delete the physical file
            var physicalPath = Path.Combine(_environment.WebRootPath,
                document.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(physicalPath))
            {
                System.IO.File.Delete(physicalPath);
            }

            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "Document",
                EntityId = document.UnitId ?? 0,
                Action = "BuyerLawyerDelete",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = "Lawyer",
                OldValues = JsonSerializer.Serialize(new
                {
                    document.FileName,
                    DocumentType = document.DocumentType.ToString()
                }),
                Timestamp = DateTime.UtcNow
            });

            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Buyer's lawyer {UserId} deleted document {DocumentId} for Unit {UnitId}",
                userId, id, document.UnitId);

            TempData["Success"] = "Document deleted successfully.";

            if (assignment != null)
                return RedirectToAction(nameof(ReviewUnit), new { id = assignment.Id });

            return RedirectToAction(nameof(Dashboard));
        }

        // GET: /BuyerLawyer/DownloadDocument/5
        public async Task<IActionResult> DownloadDocument(int id)
        {
            var userId = _userManager.GetUserId(User);

            var document = await _context.Documents
                .Include(d => d.Unit)
                    .ThenInclude(u => u!.LawyerAssignments)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
                return NotFound();

            // Verify this lawyer has a PurchaserLawyer assignment on the document's unit
            var hasAssignment = document.Unit?.LawyerAssignments
                .Any(la => la.LawyerId == userId
                         && la.Role == LawyerRole.PurchaserLawyer
                         && la.IsActive) ?? false;

            if (!hasAssignment)
                return Forbid();

            var physicalPath = Path.Combine(_environment.WebRootPath,
                document.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            if (!System.IO.File.Exists(physicalPath))
            {
                TempData["Error"] = "File not found on server.";
                return RedirectToAction(nameof(Dashboard));
            }

            return PhysicalFile(physicalPath, document.ContentType, document.FileName);
        }

        // GET: /BuyerLawyer/DownloadSOA/5 (unitId)
        public async Task<IActionResult> DownloadSOA(int id)
        {
            var userId = _userManager.GetUserId(User);

            var assignment = await _context.LawyerAssignments
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Project)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.SOA)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Deposits)
                        .ThenInclude(d => d.InterestPeriods)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Purchasers)
                        .ThenInclude(p => p.Purchaser)
                .FirstOrDefaultAsync(la => la.UnitId == id
                                        && la.LawyerId == userId
                                        && la.Role == LawyerRole.PurchaserLawyer);

            if (assignment == null)
                return Forbid();

            var unit = assignment.Unit;

            if (unit.SOA == null)
            {
                TempData["Error"] = "Statement of Adjustments has not been generated.";
                return RedirectToAction(nameof(ReviewUnit), new { id = assignment.Id });
            }

            // Get purchaser names
            var primaryPurchaser = unit.Purchasers.FirstOrDefault(p => p.IsPrimaryPurchaser);
            var purchaserName = primaryPurchaser != null
                ? $"{primaryPurchaser.Purchaser.FirstName} {primaryPurchaser.Purchaser.LastName}"
                : "Unknown";

            var coPurchasers = unit.Purchasers
                .Where(p => !p.IsPrimaryPurchaser)
                .Select(p => $"{p.Purchaser.FirstName} {p.Purchaser.LastName}");
            var coPurchaserNames = string.Join(", ", coPurchasers);

            // Generate PDF
            var pdfBytes = _pdfService.GenerateStatementOfAdjustments(
                unit,
                unit.SOA,
                unit.Deposits.ToList(),
                purchaserName,
                coPurchaserNames
            );

            var fileName = $"SOA_{unit.Project.Name.Replace(" ", "_")}_{unit.UnitNumber}_{DateTime.Now:yyyyMMdd}.pdf";

            _logger.LogInformation("Buyer's lawyer {UserId} downloaded SOA for Unit {UnitId}", userId, unit.Id);

            return File(pdfBytes, "application/pdf", fileName);
        }

        // GET: /BuyerLawyer/AcceptInvitation
        [AllowAnonymous]
        public async Task<IActionResult> AcceptInvitation(string email, string code)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(code))
            {
                return View("InvalidInvitation");
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return View("InvalidInvitation");
            }

            if (user.EmailConfirmed)
            {
                TempData["Info"] = "Your account is already activated. Please log in.";
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            // Get PurchaserLawyer assignments to show on welcome page
            var assignments = await _context.LawyerAssignments
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Project)
                .Where(la => la.LawyerId == user.Id
                          && la.Role == LawyerRole.PurchaserLawyer)
                .ToListAsync();

            var viewModel = new LawyerAcceptInvitationViewModel
            {
                Email = email,
                Code = code,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FirmName = user.CompanyName ?? user.LawFirm,
                AssignedUnitsCount = assignments.Count,
                ProjectNames = assignments.Select(a => a.Unit.Project.Name).Distinct().ToList()
            };

            return View(viewModel);
        }

        // POST: /BuyerLawyer/AcceptInvitation
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptInvitation(LawyerAcceptInvitationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "Invalid invitation link.");
                return View(model);
            }

            // Reset password using the invitation code
            var result = await _userManager.ResetPasswordAsync(user, model.Code, model.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    if (error.Code == "InvalidToken")
                    {
                        ModelState.AddModelError("",
                            "This invitation link has expired or is invalid. Please contact the purchaser for a new invitation.");
                    }
                    else
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                }
                return View(model);
            }

            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);

            await _signInManager.SignInAsync(user, isPersistent: false);

            _logger.LogInformation("Buyer's lawyer {Email} accepted invitation and activated account", model.Email);

            TempData["Success"] = "Welcome! Your account has been activated successfully.";
            return RedirectToAction(nameof(Dashboard));
        }

        // GET: /BuyerLawyer/InvalidInvitation
        [AllowAnonymous]
        public IActionResult InvalidInvitation()
        {
            return View();
        }
    }
}
