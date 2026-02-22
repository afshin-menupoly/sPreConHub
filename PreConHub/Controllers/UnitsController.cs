using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PreConHub.Data;
using PreConHub.Models.Entities;
using PreConHub.Models.ViewModels;
using PreConHub.Services;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Text;

namespace PreConHub.Controllers
{
    [Authorize(Roles = "Admin,Builder")]
    public class UnitsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISoaCalculationService _soaService;
        private readonly IShortfallAnalysisService _shortfallService;
        private readonly IEmailService _emailService;
        private readonly IPdfService _pdfService;
        private readonly ILogger<UnitsController> _logger;
        private readonly IDocumentAnalysisService _documentAnalysisService;
        private readonly INotificationService _notificationService;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public UnitsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ISoaCalculationService soaService,
            IShortfallAnalysisService shortfallService,
            IEmailService emailService,
            IPdfService pdfService,
            IDocumentAnalysisService documentAnalysisService,
            INotificationService notificationService,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<UnitsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _soaService = soaService;
            _shortfallService = shortfallService;
            _emailService = emailService;
            _pdfService = pdfService;
            _documentAnalysisService = documentAnalysisService;
            _notificationService = notificationService;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        // GET: /Units/Index/5 (ProjectId)
        public async Task<IActionResult> Index(int id)
        {
            var project = await _context.Projects
                .Include(p => p.Units)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && project.BuilderId != userId)
                return Forbid();

            ViewBag.ProjectId = id;
            ViewBag.ProjectName = project.Name;

            return View(project.Units.OrderBy(u => u.UnitNumber).ToList());
        }

        // GET: /Units/Create/5 (ProjectId)
        public async Task<IActionResult> Create(int id)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && project.BuilderId != userId)
                return Forbid();

            var viewModel = new CreateUnitViewModel
            {
                ProjectId = id
            };

            ViewBag.ProjectName = project.Name;
            ViewBag.UnitTypes = new SelectList(Enum.GetValues(typeof(UnitType)));
            ViewBag.MaxUnits = project.MaxUnits;
            ViewBag.CurrentUnitCount = await _context.Units.CountAsync(u => u.ProjectId == id);

            return View(viewModel);
        }

        // POST: /Units/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUnitViewModel model)
        {
            var project = await _context.Projects.FindAsync(model.ProjectId);
            if (project == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && project.BuilderId != userId)
                return Forbid();

            // Quota enforcement: non-admin builders check MaxUnits
            if (!User.IsInRole("Admin"))
            {
                var currentUnits = await _context.Units.CountAsync(u => u.ProjectId == model.ProjectId);
                if (project.MaxUnits == null)
                {
                    TempData["Error"] = "Unit quota has not been assigned for this project. Contact an administrator.";
                    return RedirectToAction("Dashboard", "Projects", new { id = model.ProjectId });
                }
                if (currentUnits >= project.MaxUnits.Value)
                {
                    TempData["Error"] = $"Unit limit reached ({project.MaxUnits} units). Contact an administrator to increase your quota.";
                    return RedirectToAction("Dashboard", "Projects", new { id = model.ProjectId });
                }
            }

            // Check if unit number already exists in this project
            var existingUnit = await _context.Units
                .AnyAsync(u => u.ProjectId == model.ProjectId && u.UnitNumber == model.UnitNumber);

            if (existingUnit)
            {
                ModelState.AddModelError("UnitNumber", "This unit number already exists in the project.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.ProjectName = project.Name;
                ViewBag.UnitTypes = new SelectList(Enum.GetValues(typeof(UnitType)));
                return View(model);
            }

            var unit = new Unit
            {
                ProjectId = model.ProjectId,
                UnitNumber = model.UnitNumber,
                FloorNumber = model.FloorNumber,
                UnitType = model.UnitType,
                Bedrooms = model.Bedrooms,
                Bathrooms = model.Bathrooms,
                SquareFootage = model.SquareFootage,
                PurchasePrice = model.PurchasePrice,
                HasParking = model.HasParking,
                ParkingPrice = model.HasParking ? model.ParkingPrice : 0,
                HasLocker = model.HasLocker,
                LockerPrice = model.HasLocker ? model.LockerPrice : 0,
                OccupancyDate = model.OccupancyDate ?? project.OccupancyDate,
                ClosingDate = model.ClosingDate ?? project.ClosingDate,
                Status = UnitStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.Units.Add(unit);
            await _context.SaveChangesAsync();

            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "Unit",
                EntityId = unit.Id,
                Action = "Create",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = User.IsInRole("Admin") ? "Admin" : "Builder",
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { unit.UnitNumber, unit.UnitType, unit.PurchasePrice, unit.ProjectId }),
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            _logger.LogInformation("Unit {UnitNumber} created for Project {ProjectId}", unit.UnitNumber, model.ProjectId);

            TempData["Success"] = $"Unit {unit.UnitNumber} created successfully.";

            // Check if user wants to add another
            if (Request.Form.ContainsKey("saveAndAddAnother"))
            {
                return RedirectToAction(nameof(Create), new { id = model.ProjectId });
            }

            return RedirectToAction("Dashboard", "Projects", new { id = model.ProjectId });
        }

        // GET: /Units/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var unit = await _context.Units
                .Include(u => u.Project)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (unit == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && unit.Project.BuilderId != userId)
                return Forbid();

            var viewModel = new CreateUnitViewModel
            {
                ProjectId = unit.ProjectId,
                UnitNumber = unit.UnitNumber,
                FloorNumber = unit.FloorNumber,
                UnitType = unit.UnitType,
                Bedrooms = unit.Bedrooms,
                Bathrooms = unit.Bathrooms,
                SquareFootage = unit.SquareFootage,
                PurchasePrice = unit.PurchasePrice,
                HasParking = unit.HasParking,
                ParkingPrice = unit.ParkingPrice,
                HasLocker = unit.HasLocker,
                LockerPrice = unit.LockerPrice,
                OccupancyDate = unit.OccupancyDate,
                ClosingDate = unit.ClosingDate,
                ActualAnnualLandTax = unit.ActualAnnualLandTax,
                ActualMonthlyMaintenanceFee = unit.ActualMonthlyMaintenanceFee
            };

            ViewBag.ProjectName = unit.Project.Name;
            ViewBag.UnitId = unit.Id;

            return View(viewModel);
        }

        // POST: /Units/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CreateUnitViewModel model)
        {
            var unit = await _context.Units
                .Include(u => u.Project)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (unit == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && unit.Project.BuilderId != userId)
                return Forbid();

            // Check if unit number already exists (excluding current unit)
            var existingUnit = await _context.Units
                .AnyAsync(u => u.ProjectId == model.ProjectId 
                            && u.UnitNumber == model.UnitNumber 
                            && u.Id != id);

            if (existingUnit)
            {
                ModelState.AddModelError("UnitNumber", "This unit number already exists in the project.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.ProjectName = unit.Project.Name;
                ViewBag.UnitId = id;
                return View(model);
            }

            var oldClosingDate = unit.ClosingDate;

            unit.UnitNumber = model.UnitNumber;
            unit.FloorNumber = model.FloorNumber;
            unit.UnitType = model.UnitType;
            unit.Bedrooms = model.Bedrooms;
            unit.Bathrooms = model.Bathrooms;
            unit.SquareFootage = model.SquareFootage;
            unit.PurchasePrice = model.PurchasePrice;
            unit.HasParking = model.HasParking;
            unit.ParkingPrice = model.HasParking ? model.ParkingPrice : 0;
            unit.HasLocker = model.HasLocker;
            unit.LockerPrice = model.HasLocker ? model.LockerPrice : 0;
            unit.OccupancyDate = model.OccupancyDate;
            unit.ClosingDate = model.ClosingDate;
            unit.ActualAnnualLandTax = model.ActualAnnualLandTax;
            unit.ActualMonthlyMaintenanceFee = model.ActualMonthlyMaintenanceFee;
            unit.UpdatedAt = DateTime.UtcNow;

            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "Unit",
                EntityId = unit.Id,
                Action = "Edit",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = User.IsInRole("Admin") ? "Admin" : "Builder",
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { unit.UnitNumber, unit.PurchasePrice, unit.ClosingDate }),
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            // Auto-recalculate SOA when closing date changes (spec Process B & C)
            if (oldClosingDate != model.ClosingDate)
            {
                try
                {
                    await _soaService.CalculateSOAAsync(id);
                    await _shortfallService.AnalyzeShortfallAsync(id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error auto-recalculating SOA for unit {UnitId} after closing date change", id);
                }
            }

            TempData["Success"] = $"Unit {unit.UnitNumber} updated successfully.";
            return RedirectToAction("Dashboard", "Projects", new { id = unit.ProjectId });
        }

        // GET: /Units/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var unit = await _context.Units
                .Include(u => u.Project)
                .Include(u => u.Deposits)
                    .ThenInclude(d => d.InterestPeriods)
                .Include(u => u.Purchasers)
                    .ThenInclude(p => p.Purchaser)
                .Include(u => u.Purchasers)
                    .ThenInclude(p => p.MortgageInfo)
                .Include(u => u.Purchasers)
                    .ThenInclude(p => p.Financials)
                .Include(u => u.SOA)
                .Include(u => u.ShortfallAnalysis)
                .Include(u => u.Documents)
                .Include(u => u.ExtensionRequests)
                    .ThenInclude(er => er.RequestedByPurchaser)
                .Include(u => u.LawyerAssignments)
                    .ThenInclude(la => la.Lawyer)
                .Include(u => u.LawyerAssignments)
                    .ThenInclude(la => la.LawyerNotes)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (unit == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && unit.Project.BuilderId != userId)
                return Forbid();

            var viewModel = new UnitDetailsViewModel
            {
                UnitId = unit.Id,
                UnitNumber = unit.UnitNumber,
                ProjectName = unit.Project.Name,
                ProjectId = unit.ProjectId,
                UnitType = unit.UnitType,
                Bedrooms = unit.Bedrooms,
                Bathrooms = unit.Bathrooms,
                SquareFootage = unit.SquareFootage,
                PurchasePrice = unit.PurchasePrice,
                CurrentAppraisalValue = unit.CurrentAppraisalValue,
                OccupancyDate = unit.OccupancyDate,
                ClosingDate = unit.ClosingDate,
                Status = unit.Status,
                Recommendation = unit.Recommendation,
                IsConfirmedByLawyer = unit.IsConfirmedByLawyer,

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
                    Holder = d.Holder.ToString(),
                    IsInterestEligible = d.IsInterestEligible,
                    InterestRate = d.InterestRate,
                    CompoundingType = d.CompoundingType,
                    InterestPeriods = d.InterestPeriods
                        .OrderBy(p => p.PeriodStart)
                        .Select(p => new DepositInterestPeriodViewModel
                        {
                            Id = p.Id,
                            DepositId = p.DepositId,
                            DepositName = d.DepositName,
                            UnitId = unit.Id,
                            PeriodStart = p.PeriodStart,
                            PeriodEnd = p.PeriodEnd,
                            AnnualRate = p.AnnualRate
                        }).ToList()
                }).ToList(),

                // Documents
                Documents = unit.Documents.Select(d => new DocumentViewModel
                {
                    Id = d.Id,
                    FileName = d.FileName,
                    DocumentType = d.DocumentType,
                    DocumentTypeName = d.DocumentType.ToString(),
                    Description = d.Description,
                    FileSize = d.FileSize,
                    UploadedAt = d.UploadedAt
                }).OrderByDescending(d => d.UploadedAt).ToList(),

                // Purchasers
                AllPurchasers = unit.Purchasers.Select(up => new PurchaserInfoViewModel
                {
                    PurchaserId = up.PurchaserId,
                    FullName = $"{up.Purchaser.FirstName} {up.Purchaser.LastName}",
                    Email = up.Purchaser.Email ?? "",
                    Phone = up.Purchaser.PhoneNumber,
                    IsPrimary = up.IsPrimaryPurchaser,
                    OwnershipPercentage = up.OwnershipPercentage,
                    HasMortgageApproval = up.MortgageInfo?.HasMortgageApproval ?? false,
                    ApprovalType = up.MortgageInfo?.ApprovalType ?? default,
                    MortgageProvider = User.IsInRole("Admin") ? up.MortgageInfo?.MortgageProvider : null,
                    MortgageAmount = User.IsInRole("Admin") ? up.MortgageInfo?.ApprovedAmount : null,
                    ApprovalExpiryDate = User.IsInRole("Admin") ? up.MortgageInfo?.ApprovalExpiryDate : null,
                    AdditionalCashAvailable = User.IsInRole("Admin") ? up.Financials?.AdditionalCashAvailable : null,
                    CreditScore = up.MortgageInfo?.CreditScore
                }).ToList(),

                PrimaryPurchaser = unit.Purchasers
                    .Where(up => up.IsPrimaryPurchaser)
                    .Select(up => new PurchaserInfoViewModel
                    {
                        PurchaserId = up.PurchaserId,
                        FullName = $"{up.Purchaser.FirstName} {up.Purchaser.LastName}",
                        Email = up.Purchaser.Email ?? "",
                        Phone = up.Purchaser.PhoneNumber,
                        IsPrimary = up.IsPrimaryPurchaser,
                        OwnershipPercentage = up.OwnershipPercentage,
                        HasMortgageApproval = up.MortgageInfo?.HasMortgageApproval ?? false,
                        ApprovalType = up.MortgageInfo?.ApprovalType ?? default,
                        MortgageProvider = User.IsInRole("Admin") ? up.MortgageInfo?.MortgageProvider : null,
                        MortgageAmount = User.IsInRole("Admin") ? up.MortgageInfo?.ApprovedAmount : null,
                        ApprovalExpiryDate = User.IsInRole("Admin") ? up.MortgageInfo?.ApprovalExpiryDate : null,
                        AdditionalCashAvailable = User.IsInRole("Admin") ? up.Financials?.AdditionalCashAvailable : null,
                        CreditScore = up.MortgageInfo?.CreditScore
                    }).FirstOrDefault(),

                // ===== ADD LAWYER ASSIGNMENTS MAPPING =====
                LawyerAssignments = unit.LawyerAssignments
                    .Where(la => la.IsActive)
                    .Select(la => new LawyerAssignmentViewModel
                    {
                        AssignmentId = la.Id,
                        LawyerId = la.LawyerId,
                        LawyerName = $"{la.Lawyer.FirstName} {la.Lawyer.LastName}",
                        LawyerEmail = la.Lawyer.Email ?? "",
                        LawyerPhone = la.Lawyer.PhoneNumber,
                        LawFirm = la.Lawyer.CompanyName,
                        Role = la.Role,
                        ReviewStatus = la.ReviewStatus,
                        AssignedAt = la.AssignedAt,
                        ReviewedAt = la.ReviewedAt,
                        IsActive = la.IsActive,
                        BuilderNotes = la.LawyerNotes
                            .Where(n => n.Visibility == NoteVisibility.ForBuilder)
                            .OrderByDescending(n => n.CreatedAt)
                            .Select(n => new LawyerNoteViewModel
                            {
                                NoteId = n.Id,
                                Note = n.Note,
                                NoteType = n.NoteType,
                                CreatedAt = n.CreatedAt,
                                IsRead = n.IsReadByBuilder,
                                LawyerName = $"{la.Lawyer.FirstName} {la.Lawyer.LastName}"
                            }).ToList(),
                        UnreadNotesCount = la.LawyerNotes
                            .Count(n => n.Visibility == NoteVisibility.ForBuilder && !n.IsReadByBuilder)
                    }).ToList(),
                TotalUnreadLawyerNotes = unit.LawyerAssignments
                    .SelectMany(la => la.LawyerNotes)
                    .Count(n => n.Visibility == NoteVisibility.ForBuilder && !n.IsReadByBuilder),

                // Extension Requests
                ExtensionRequests = unit.ExtensionRequests
                    .OrderByDescending(er => er.RequestedDate)
                    .Select(er => new ExtensionRequestItem
                    {
                        RequestId = er.Id,
                        UnitId = er.UnitId,
                        UnitNumber = unit.UnitNumber,
                        ProjectName = unit.Project.Name,
                        PurchaserName = $"{er.RequestedByPurchaser.FirstName} {er.RequestedByPurchaser.LastName}".Trim(),
                        OriginalClosingDate = er.OriginalClosingDate,
                        RequestedNewClosingDate = er.RequestedNewClosingDate,
                        Reason = er.Reason,
                        RequestedDate = er.RequestedDate,
                        Status = er.Status,
                        ReviewerNotes = er.ReviewerNotes,
                        ReviewedAt = er.ReviewedAt
                    }).ToList()
            };

            if (unit.SOA != null)
            {
                viewModel.SOA = new SOAViewModel
                {
                    PurchasePrice = unit.SOA.PurchasePrice,
                    LandTransferTax = unit.SOA.LandTransferTax,
                    TorontoLandTransferTax = unit.SOA.TorontoLandTransferTax,
                    DevelopmentCharges = unit.SOA.DevelopmentCharges,
                    TarionFee = unit.SOA.TarionFee,
                    UtilityConnectionFees = unit.SOA.UtilityConnectionFees,
                    PropertyTaxAdjustment = unit.SOA.PropertyTaxAdjustment,
                    CommonExpenseAdjustment = unit.SOA.CommonExpenseAdjustment,
                    OccupancyFeesOwing = unit.SOA.OccupancyFeesOwing,
                    ParkingPrice = unit.SOA.ParkingPrice,
                    LockerPrice = unit.SOA.LockerPrice,
                    Upgrades = unit.SOA.Upgrades,
                    LegalFeesEstimate = unit.SOA.LegalFeesEstimate,
                    OtherDebits = unit.SOA.OtherDebits,
                    TotalDebits = unit.SOA.TotalDebits,
                    DepositsPaid = unit.SOA.DepositsPaid,
                    DepositInterest = unit.SOA.DepositInterest,
                    BuilderCredits = unit.SOA.BuilderCredits,
                    OtherCredits = unit.SOA.OtherCredits,
                    TotalCredits = unit.SOA.TotalCredits,
                    BalanceDueOnClosing = unit.SOA.BalanceDueOnClosing,
                    MortgageAmount = unit.SOA.MortgageAmount,
                    CashRequiredToClose = unit.SOA.CashRequiredToClose,
                    CalculatedAt = unit.SOA.CalculatedAt,
                    IsConfirmedByLawyer = unit.SOA.IsConfirmedByLawyer,
                    IsConfirmedByBuilder = unit.SOA.IsConfirmedByBuilder,
                    IsLocked = unit.SOA.IsLocked,
                    LawyerNotes = unit.SOA.LawyerNotes,
                    LawyerUploadedBalanceDue = unit.SOA.LawyerUploadedBalanceDue
                };
            }

            if (unit.ShortfallAnalysis != null)
            {
                viewModel.Shortfall = new ShortfallViewModel
                {
                    SOAAmount = unit.ShortfallAnalysis.SOAAmount,
                    MortgageApproved = unit.ShortfallAnalysis.MortgageApproved,
                    DepositsPaid = unit.ShortfallAnalysis.DepositsPaid,
                    AdditionalCashAvailable = unit.ShortfallAnalysis.AdditionalCashAvailable,
                    TotalFundsAvailable = unit.ShortfallAnalysis.TotalFundsAvailable,
                    ShortfallAmount = unit.ShortfallAnalysis.ShortfallAmount,
                    ShortfallPercentage = unit.ShortfallAnalysis.ShortfallPercentage,
                    RiskLevel = unit.ShortfallAnalysis.RiskLevel,
                    Recommendation = unit.ShortfallAnalysis.Recommendation,
                    SuggestedDiscount = unit.ShortfallAnalysis.SuggestedDiscount,
                    SuggestedVTBAmount = unit.ShortfallAnalysis.SuggestedVTBAmount,
                    RecommendationReasoning = unit.ShortfallAnalysis.RecommendationReasoning,
                    CalculatedAt = unit.ShortfallAnalysis.CalculatedAt
                };
            }

            return View(viewModel);
        }

        // POST: /Units/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var unit = await _context.Units
                .Include(u => u.Project)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (unit == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && unit.Project.BuilderId != userId)
                return Forbid();

            var projectId = unit.ProjectId;
            var unitNumber = unit.UnitNumber;

            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "Unit",
                EntityId = unit.Id,
                Action = "Delete",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = User.IsInRole("Admin") ? "Admin" : "Builder",
                OldValues = System.Text.Json.JsonSerializer.Serialize(new { unit.UnitNumber, unit.PurchasePrice, unit.ProjectId }),
                Timestamp = DateTime.UtcNow
            });
            _context.Units.Remove(unit);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Unit {UnitNumber} deleted from Project {ProjectId}", unitNumber, projectId);

            TempData["Success"] = $"Unit {unitNumber} deleted successfully.";
            return RedirectToAction("Dashboard", "Projects", new { id = projectId });
        }

        // POST: /Units/CalculateSOA/5
        [HttpPost]
        public async Task<IActionResult> CalculateSOA(int id)
        {
            var userId = _userManager.GetUserId(User);
            try
            {
                var soa = await _soaService.CalculateSOAAsync(id);
                var analysis = await _shortfallService.AnalyzeShortfallAsync(id);

                _context.AuditLogs.Add(new AuditLog
                {
                    EntityType = "StatementOfAdjustments",
                    EntityId = id,
                    Action = "Trigger",
                    UserId = userId,
                    UserName = User.Identity?.Name,
                    UserRole = User.IsInRole("Admin") ? "Admin" : "Builder",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                TempData["Success"] = "SOA and shortfall analysis calculated successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating SOA for unit {UnitId}", id);
                TempData["Error"] = "Error calculating SOA: " + ex.Message;
            }

            var unit = await _context.Units.FindAsync(id);
            return RedirectToAction("Dashboard", "Projects", new { id = unit?.ProjectId });
        }

        // GET: /Units/AddDeposit/5
        public async Task<IActionResult> AddDeposit(int id)
        {
            var unit = await _context.Units
                .Include(u => u.Project)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (unit == null)
                return NotFound();

            ViewBag.UnitId = id;
            ViewBag.UnitNumber = unit.UnitNumber;
            ViewBag.ProjectName = unit.Project.Name;

            return View();
        }

        // POST: /Units/AddDeposit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDeposit(int unitId, string depositName, decimal amount, DateTime dueDate, bool isPaid, DateTime? paidDate, string? holder, bool isInterestEligible, decimal? interestRate)
        {
            var unit = await _context.Units.FindAsync(unitId);
            if (unit == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            var deposit = new Deposit
            {
                UnitId = unitId,
                DepositName = depositName,
                Amount = amount,
                DueDate = dueDate,
                IsPaid = isPaid,
                PaidDate = isPaid ? (paidDate ?? DateTime.UtcNow) : null,
                Status = isPaid ? DepositStatus.Paid : DepositStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                Holder = ParseDepositHolder(holder),
                IsInterestEligible = isInterestEligible,
                InterestRate = interestRate.HasValue ? interestRate.Value / 100m : null
            };

            _context.Deposits.Add(deposit);
            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "Deposit",
                EntityId = unitId,
                Action = "Create",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = User.IsInRole("Admin") ? "Admin" : "Builder",
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { depositName, amount, dueDate }),
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Deposit '{depositName}' added successfully.";
            return RedirectToAction(nameof(Details), new { id = unitId });
        }

        // GET: /Units/DownloadSOA/5
        public async Task<IActionResult> DownloadSOA(int id)
        {
            var unit = await _context.Units
                .Include(u => u.Project)
                .Include(u => u.SOA)
                .Include(u => u.Deposits)
                    .ThenInclude(d => d.InterestPeriods)
                .Include(u => u.Purchasers)
                    .ThenInclude(p => p.Purchaser)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (unit == null || unit.SOA == null)
            {
                TempData["Error"] = "SOA not found. Please calculate SOA first.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var primary = unit.Purchasers.FirstOrDefault(p => p.IsPrimaryPurchaser);
            var purchaserName = primary != null
                ? $"{primary.Purchaser.FirstName} {primary.Purchaser.LastName}"
                : "Unknown";
            var coPurchaserNames = string.Join(", ", unit.Purchasers
                .Where(p => !p.IsPrimaryPurchaser)
                .Select(p => $"{p.Purchaser.FirstName} {p.Purchaser.LastName}"));

            var pdfBytes = _pdfService.GenerateStatementOfAdjustments(
                unit, unit.SOA, unit.Deposits.ToList(), purchaserName, coPurchaserNames);
            return File(pdfBytes, "application/pdf",
                $"SOA_{unit.Project.Name.Replace(" ", "_")}_{unit.UnitNumber}_{DateTime.Now:yyyyMMdd}.pdf");
        }

        // GET: /Units/AddPurchaser/5
        public async Task<IActionResult> AddPurchaser(int id)
        {
            var unit = await _context.Units
                .Include(u => u.Project)
                .Include(u => u.Purchasers)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (unit == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && unit.Project.BuilderId != userId)
                return Forbid();

            // Check if purchaser already exists
            if (unit.Purchasers.Any())
            {
                TempData["Warning"] = "This unit already has a purchaser assigned. You can edit their information instead.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ViewBag.UnitId = id;
            ViewBag.UnitNumber = unit.UnitNumber;
            ViewBag.ProjectId = unit.ProjectId;
            ViewBag.ProjectName = unit.Project.Name;

            return View();
        }

        // POST: /Units/AddPurchaser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPurchaser(
            int unitId,
            string firstName,
            string lastName,
            string email,
            string? phone,
            int ownershipPercentage,
            // Co-purchaser (optional)
            string? coFirstName,
            string? coLastName,
            string? coEmail,
            string? coPhone,
            int? coOwnershipPercentage,
            // Lawyer info (optional)
            string? lawyerName,
            string? lawFirm,
            string? lawyerEmail,
            string? lawyerPhone,
            // Options
            bool sendInvitation = true)
        {
            var unit = await _context.Units
                .Include(u => u.Project)
                .FirstOrDefaultAsync(u => u.Id == unitId);

            if (unit == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && unit.Project.BuilderId != userId)
                return Forbid();

            // Validate email
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Email address is required.";
                return RedirectToAction(nameof(AddPurchaser), new { id = unitId });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Check if user with this email already exists
                var existingUser = await _userManager.FindByEmailAsync(email);
                ApplicationUser purchaserUser;

                if (existingUser != null)
                {
                    purchaserUser = existingUser;
                }
                else
                {
                    // Create a new user account for the purchaser
                    purchaserUser = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        FirstName = firstName,
                        LastName = lastName,
                        PhoneNumber = phone,
                        UserType = UserType.Purchaser,
                        CreatedByUserId = userId,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        EmailConfirmed = false // Will be confirmed when they register
                    };

                    // Create with a temporary password (they'll reset it)
                    var tempPassword = GenerateTemporaryPassword();
                    var createResult = await _userManager.CreateAsync(purchaserUser, tempPassword);

                    if (!createResult.Succeeded)
                    {
                        var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                        TempData["Error"] = $"Failed to create purchaser account: {errors}";
                        return RedirectToAction(nameof(AddPurchaser), new { id = unitId });
                    }

                    // Assign Purchaser role
                    await _userManager.AddToRoleAsync(purchaserUser, "Purchaser");
                }

                // Create UnitPurchaser link
                var unitPurchaser = new UnitPurchaser
                {
                    UnitId = unitId,
                    PurchaserId = purchaserUser.Id,
                    IsPrimaryPurchaser = true,
                    OwnershipPercentage = ownershipPercentage,
                    CreatedAt = DateTime.UtcNow
                };

                _context.UnitPurchasers.Add(unitPurchaser);

                // Handle Co-Purchaser if provided
                if (!string.IsNullOrWhiteSpace(coEmail) && !string.IsNullOrWhiteSpace(coFirstName))
                {
                    var coExistingUser = await _userManager.FindByEmailAsync(coEmail);
                    ApplicationUser coPurchaserUser;

                    if (coExistingUser != null)
                    {
                        coPurchaserUser = coExistingUser;
                    }
                    else
                    {
                        coPurchaserUser = new ApplicationUser
                        {
                            UserName = coEmail,
                            Email = coEmail,
                            FirstName = coFirstName,
                            LastName = coLastName ?? "",
                            PhoneNumber = coPhone,
                            UserType = UserType.Purchaser,
                            CreatedByUserId = userId,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            EmailConfirmed = false
                        };

                        var coTempPassword = GenerateTemporaryPassword();
                        var coCreateResult = await _userManager.CreateAsync(coPurchaserUser, coTempPassword);

                        if (coCreateResult.Succeeded)
                        {
                            await _userManager.AddToRoleAsync(coPurchaserUser, "Purchaser");
                        }
                    }

                    var coUnitPurchaser = new UnitPurchaser
                    {
                        UnitId = unitId,
                        PurchaserId = coPurchaserUser.Id,
                        IsPrimaryPurchaser = false,
                        OwnershipPercentage = coOwnershipPercentage ?? 50,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.UnitPurchasers.Add(coUnitPurchaser);
                }

                // Handle Lawyer assignment if provided
                if (!string.IsNullOrWhiteSpace(lawyerEmail))
                {
                    var lawyerUser = await _userManager.FindByEmailAsync(lawyerEmail);

                    if (lawyerUser == null)
                    {
                        // Create lawyer user
                        lawyerUser = new ApplicationUser
                        {
                            UserName = lawyerEmail,
                            Email = lawyerEmail,
                            FirstName = lawyerName?.Split(' ').FirstOrDefault() ?? "",
                            LastName = lawyerName?.Split(' ').Skip(1).FirstOrDefault() ?? "",
                            PhoneNumber = lawyerPhone,
                            UserType = UserType.Lawyer,
                            CompanyName = lawFirm,
                            CreatedByUserId = userId,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            EmailConfirmed = false
                        };

                        var lawyerTempPassword = GenerateTemporaryPassword();
                        var lawyerCreateResult = await _userManager.CreateAsync(lawyerUser, lawyerTempPassword);

                        if (lawyerCreateResult.Succeeded)
                        {
                            await _userManager.AddToRoleAsync(lawyerUser, "Lawyer");
                        }
                    }

                    // Create lawyer assignment
                    var lawyerAssignment = new LawyerAssignment
                    {
                        ProjectId = unit.ProjectId,
                        LawyerId = lawyerUser.Id,
                        AssignedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    _context.LawyerAssignments.Add(lawyerAssignment);
                }

                // Update unit status
                unit.Status = UnitStatus.Pending; // Will update to DataComplete when purchaser submits info
                unit.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _notificationService.NotifyPurchaserAddedAsync(
                    unitId: unitId,
                    purchaserName: $"{firstName} {lastName}",
                    builderId: unit.Project.BuilderId
                );

                // Generate invitation token/link
                var invitationToken = await _userManager.GeneratePasswordResetTokenAsync(purchaserUser);
                var encodedToken = System.Net.WebUtility.UrlEncode(invitationToken);

                // Store the invitation link in TempData for display
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var invitationLink = $"{baseUrl}/Purchaser/AcceptInvitation?email={System.Net.WebUtility.UrlEncode(email)}&code={encodedToken}";

                // Send invitation email
                var emailSent = await _emailService.SendPurchaserInvitationAsync(
                    purchaserUser.Email!,
                    $"{purchaserUser.FirstName} {purchaserUser.LastName}",
                    unit.UnitNumber,
                    unit.Project.Name,
                    invitationLink
                );

                if (emailSent)
                {
                    _logger.LogInformation("Purchaser invitation email sent to {Email}", purchaserUser.Email);
                }

                TempData["InvitationLink"] = invitationLink;
                TempData["EmailSent"] = emailSent; // Add this for UI feedback
                if (sendInvitation)
                {
                    // TODO: Send actual email
                    // For now, show the link to copy
                    TempData["InvitationLink"] = invitationLink;
                    TempData["PurchaserEmail"] = email;
                }

                _logger.LogInformation("Purchaser {Email} added to Unit {UnitId}", email, unitId);
                TempData["Success"] = $"Purchaser {firstName} {lastName} added successfully!";

                return RedirectToAction(nameof(PurchaserAdded), new { id = unitId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error adding purchaser to unit {UnitId}", unitId);
                TempData["Error"] = "An error occurred while adding the purchaser. Please try again.";
                return RedirectToAction(nameof(AddPurchaser), new { id = unitId });
            }
        }

        // GET: /Units/PurchaserAdded/5 - Confirmation page with invitation link
        public async Task<IActionResult> PurchaserAdded(int id)
        {
            var unit = await _context.Units
                .Include(u => u.Project)
                .Include(u => u.Purchasers)
                    .ThenInclude(p => p.Purchaser)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (unit == null)
                return NotFound();

            ViewBag.UnitId = id;
            ViewBag.UnitNumber = unit.UnitNumber;
            ViewBag.ProjectId = unit.ProjectId;
            ViewBag.ProjectName = unit.Project.Name;
            ViewBag.InvitationLink = TempData["InvitationLink"];
            ViewBag.PurchaserEmail = TempData["PurchaserEmail"];

            var primaryPurchaser = unit.Purchasers.FirstOrDefault(p => p.IsPrimaryPurchaser);
            if (primaryPurchaser != null)
            {
                ViewBag.PurchaserName = $"{primaryPurchaser.Purchaser.FirstName} {primaryPurchaser.Purchaser.LastName}";
            }

            return View();
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

        // GET: /Units/EditPurchaser/5
        public async Task<IActionResult> EditPurchaser(int id)
        {
            var unitPurchaser = await _context.UnitPurchasers
                .Include(up => up.Unit)
                    .ThenInclude(u => u.Project)
                .Include(up => up.Purchaser)
                .Include(up => up.MortgageInfo)
                .Include(up => up.Financials)
                .FirstOrDefaultAsync(up => up.Id == id);

            if (unitPurchaser == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && unitPurchaser.Unit.Project.BuilderId != userId)
                return Forbid();

            ViewBag.UnitPurchaserId = id;
            ViewBag.UnitId = unitPurchaser.UnitId;
            ViewBag.UnitNumber = unitPurchaser.Unit.UnitNumber;
            ViewBag.ProjectName = unitPurchaser.Unit.Project.Name;

            return View(unitPurchaser);
        }

        // POST: /Units/RemovePurchaser/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemovePurchaser(int id)
        {
            var unitPurchaser = await _context.UnitPurchasers
                .Include(up => up.Unit)
                    .ThenInclude(u => u.Project)
                .Include(up => up.Purchaser)
                .FirstOrDefaultAsync(up => up.Id == id);

            if (unitPurchaser == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && unitPurchaser.Unit.Project.BuilderId != userId)
                return Forbid();

            var unitId = unitPurchaser.UnitId;
            var purchaserName = $"{unitPurchaser.Purchaser.FirstName} {unitPurchaser.Purchaser.LastName}";

            _context.UnitPurchasers.Remove(unitPurchaser);

            // Update unit status if no more purchasers
            var remainingPurchasers = await _context.UnitPurchasers
                .CountAsync(up => up.UnitId == unitId && up.Id != id);

            if (remainingPurchasers == 0)
            {
                var unit = await _context.Units.FindAsync(unitId);
                if (unit != null)
                {
                    unit.Status = UnitStatus.Pending;
                    unit.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Purchaser {purchaserName} removed from unit.";
            return RedirectToAction(nameof(Details), new { id = unitId });
        }

        // POST: /Units/ResendInvitation/5 (UnitPurchaserId)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendInvitation(int id)
        {
            var unitPurchaser = await _context.UnitPurchasers
                .Include(up => up.Unit)
                    .ThenInclude(u => u.Project)
                .Include(up => up.Purchaser)
                .FirstOrDefaultAsync(up => up.Id == id);

            if (unitPurchaser == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && unitPurchaser.Unit.Project.BuilderId != userId)
                return Forbid();

            var purchaser = unitPurchaser.Purchaser;

            if (purchaser.EmailConfirmed)
            {
                TempData["Warning"] = $"{purchaser.FirstName} {purchaser.LastName} has already activated their account.";
                return RedirectToAction("Purchasers", "Projects", new { id = unitPurchaser.Unit.ProjectId });
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(purchaser);
            var encodedToken = System.Net.WebUtility.UrlEncode(token);

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var invitationLink = $"{baseUrl}/Purchaser/AcceptInvitation?email={System.Net.WebUtility.UrlEncode(purchaser.Email!)}&code={encodedToken}";

            var emailSent = await _emailService.SendPurchaserInvitationAsync(
                purchaser.Email!,
                $"{purchaser.FirstName} {purchaser.LastName}",
                unitPurchaser.Unit.UnitNumber,
                unitPurchaser.Unit.Project.Name,
                invitationLink
            );

            TempData["EmailSent"] = emailSent;

            TempData["InvitationLink"] = invitationLink;
            TempData["Success"] = $"New invitation link generated for {purchaser.FirstName} {purchaser.LastName}.";

            _logger.LogInformation("Invitation regenerated for {Email}", purchaser.Email);

            return RedirectToAction("Purchasers", "Projects", new { id = unitPurchaser.Unit.ProjectId });
        }

        // ============================================================
        // ADD THESE METHODS TO UnitsController.cs FOR LAWYER ASSIGNMENT
        // ============================================================

        // GET: /Units/AssignLawyer/5
        public async Task<IActionResult> AssignLawyer(int id)
        {
            var unit = await _context.Units
                .Include(u => u.Project)
                .Include(u => u.LawyerAssignments)
                    .ThenInclude(la => la.Lawyer)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (unit == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && unit.Project.BuilderId != userId)
                return Forbid();

            // Check if already has an active lawyer assignment
            var existingAssignment = unit.LawyerAssignments.FirstOrDefault(la => la.IsActive);

            ViewBag.UnitId = id;
            ViewBag.UnitNumber = unit.UnitNumber;
            ViewBag.ProjectId = unit.ProjectId;
            ViewBag.ProjectName = unit.Project.Name;
            ViewBag.ExistingLawyer = existingAssignment?.Lawyer;
            ViewBag.ExistingAssignmentId = existingAssignment?.Id;

            return View();
        }

        // POST: /Units/AssignLawyer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignLawyer(
            int unitId,
            string lawyerFirstName,
            string lawyerLastName,
            string lawyerEmail,
            string? lawyerPhone,
            string? lawFirm,
            bool sendInvitation = true)
        {
            var unit = await _context.Units
                .Include(u => u.Project)
                .Include(u => u.LawyerAssignments)
                .FirstOrDefaultAsync(u => u.Id == unitId);

            if (unit == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && unit.Project.BuilderId != userId)
                return Forbid();

            if (string.IsNullOrWhiteSpace(lawyerEmail))
            {
                TempData["Error"] = "Lawyer email is required.";
                return RedirectToAction(nameof(AssignLawyer), new { id = unitId });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Deactivate any existing assignments
                foreach (var existing in unit.LawyerAssignments.Where(la => la.IsActive))
                {
                    existing.IsActive = false;
                    existing.UpdatedAt = DateTime.UtcNow;
                }

                // Find or create lawyer user
                var lawyerUser = await _userManager.FindByEmailAsync(lawyerEmail);

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
                        return RedirectToAction(nameof(AssignLawyer), new { id = unitId });
                    }

                    await _userManager.AddToRoleAsync(lawyerUser, "Lawyer");
                }

                // Create new assignment
                var assignment = new LawyerAssignment
                {
                    UnitId = unitId,
                    ProjectId = unit.ProjectId,  // ADD THIS LINE
                    LawyerId = lawyerUser.Id,
                    AssignedAt = DateTime.UtcNow,
                    IsActive = true,
                    ReviewStatus = LawyerReviewStatus.Pending
                };

                _context.LawyerAssignments.Add(assignment);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Generate invitation link if needed
                if (sendInvitation && !lawyerUser.EmailConfirmed)
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(lawyerUser);
                    var encodedToken = System.Net.WebUtility.UrlEncode(token);

                    var baseUrl = $"{Request.Scheme}://{Request.Host}";
                    var invitationLink = $"{baseUrl}/Lawyer/AcceptInvitation?email={System.Net.WebUtility.UrlEncode(lawyerEmail)}&code={encodedToken}";

                    // Send invitation email
                    var emailSent = await _emailService.SendLawyerInvitationAsync(
                        lawyerEmail,
                        $"{lawyerFirstName} {lawyerLastName}",
                        1, // Single unit
                        new List<string> { unit.Project.Name },
                        invitationLink
                    );

                    if (emailSent)
                    {
                        _logger.LogInformation("Lawyer invitation email sent to {Email}", lawyerEmail);
                    }

                    TempData["EmailSent"] = emailSent;

                    TempData["InvitationLink"] = invitationLink;
                    TempData["LawyerEmail"] = lawyerEmail;
                }

                _logger.LogInformation("Lawyer {Email} assigned to Unit {UnitId}", lawyerEmail, unitId);
                TempData["Success"] = $"Lawyer {lawyerFirstName} {lawyerLastName} assigned successfully!";

                return RedirectToAction(nameof(LawyerAssigned), new { id = unitId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error assigning lawyer to unit {UnitId}", unitId);
                TempData["Error"] = "An error occurred while assigning the lawyer.";
                //TempData["Error"] = $"Error: {ex.Message} | Inner: {ex.InnerException?.Message}";
                return RedirectToAction(nameof(AssignLawyer), new { id = unitId });
            }
        }

        // GET: /Units/LawyerAssigned/5 - Confirmation page
        public async Task<IActionResult> LawyerAssigned(int id)
        {
            var unit = await _context.Units
                .Include(u => u.Project)
                .Include(u => u.LawyerAssignments)
                    .ThenInclude(la => la.Lawyer)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (unit == null)
                return NotFound();

            var assignment = unit.LawyerAssignments.FirstOrDefault(la => la.IsActive);

            ViewBag.UnitId = id;
            ViewBag.UnitNumber = unit.UnitNumber;
            ViewBag.ProjectId = unit.ProjectId;
            ViewBag.ProjectName = unit.Project.Name;
            ViewBag.InvitationLink = TempData["InvitationLink"];
            ViewBag.LawyerEmail = TempData["LawyerEmail"];
            ViewBag.LawyerName = assignment != null
                ? $"{assignment.Lawyer.FirstName} {assignment.Lawyer.LastName}"
                : "Lawyer";

            return View();
        }

        // POST: /Units/RemoveLawyerAssignment/5 (AssignmentId)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveLawyerAssignment(int id)
        {
            var assignment = await _context.LawyerAssignments
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Project)
                .Include(la => la.Lawyer)
                .FirstOrDefaultAsync(la => la.Id == id);

            if (assignment == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && assignment.Unit.Project.BuilderId != userId)
                return Forbid();

            var unitId = assignment.UnitId;
            var lawyerName = $"{assignment.Lawyer.FirstName} {assignment.Lawyer.LastName}";

            assignment.IsActive = false;
            assignment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Lawyer {lawyerName} removed from unit.";
            return RedirectToAction(nameof(Details), new { id = unitId });
        }

        /// <summary>
        /// Mark a lawyer note as read by builder
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNoteAsRead(int noteId, int unitId)
        {
            var note = await _context.Set<LawyerNote>()
                .Include(n => n.LawyerAssignment)
                    .ThenInclude(la => la.Unit)
                        .ThenInclude(u => u.Project)
                .FirstOrDefaultAsync(n => n.Id == noteId);

            if (note == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && note.LawyerAssignment.Unit?.Project.BuilderId != userId)
                return Forbid();

            note.IsReadByBuilder = true;
            note.ReadByBuilderAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = unitId });
        }

        /// <summary>
        /// Mark all lawyer notes for a unit as read
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllNotesAsRead(int unitId)
        {
            var unit = await _context.Units
                .Include(u => u.Project)
                .Include(u => u.LawyerAssignments)
                    .ThenInclude(la => la.LawyerNotes)
                .FirstOrDefaultAsync(u => u.Id == unitId);

            if (unit == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && unit.Project.BuilderId != userId)
                return Forbid();

            var unreadNotes = unit.LawyerAssignments
                .SelectMany(la => la.LawyerNotes)
                .Where(n => n.Visibility == NoteVisibility.ForBuilder && !n.IsReadByBuilder);

            foreach (var note in unreadNotes)
            {
                note.IsReadByBuilder = true;
                note.ReadByBuilderAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "All notes marked as read.";
            return RedirectToAction(nameof(Details), new { id = unitId });
        }



        // POST: /Units/AddDepositInterestPeriod
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDepositInterestPeriod(int depositId, int unitId, DateTime periodStart, DateTime periodEnd, decimal annualRate)
        {
            var deposit = await _context.Deposits
                .Include(d => d.Unit).ThenInclude(u => u.Project)
                .FirstOrDefaultAsync(d => d.Id == depositId);

            if (deposit == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && deposit.Unit.Project.BuilderId != userId)
                return Forbid();

            if (periodEnd <= periodStart)
            {
                TempData["Error"] = "Period end date must be after period start date.";
                return RedirectToAction(nameof(Details), new { id = unitId });
            }

            var period = new DepositInterestPeriod
            {
                DepositId = depositId,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                AnnualRate = annualRate
            };

            _context.DepositInterestPeriods.Add(period);
            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "DepositInterestPeriod",
                EntityId = depositId,
                Action = "Create",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = User.IsInRole("Admin") ? "Admin" : "Builder",
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { depositId, periodStart, periodEnd, annualRate }),
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Rate period added: {periodStart:MMM d, yyyy} – {periodEnd:MMM d, yyyy} @ {annualRate:0.000}%";
            return RedirectToAction(nameof(Details), new { id = unitId });
        }

        // POST: /Units/DeleteDepositInterestPeriod
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDepositInterestPeriod(int periodId, int unitId)
        {
            var period = await _context.DepositInterestPeriods
                .Include(p => p.Deposit).ThenInclude(d => d.Unit).ThenInclude(u => u.Project)
                .FirstOrDefaultAsync(p => p.Id == periodId);

            if (period == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && period.Deposit.Unit.Project.BuilderId != userId)
                return Forbid();

            _context.DepositInterestPeriods.Remove(period);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Rate period deleted.";
            return RedirectToAction(nameof(Details), new { id = unitId });
        }

        // GET: /Units/ReviewExtensionRequest/5 (RequestId)
        public async Task<IActionResult> ReviewExtensionRequest(int id)
        {
            var request = await _context.ClosingExtensionRequests
                .Include(r => r.Unit)
                    .ThenInclude(u => u.Project)
                .Include(r => r.RequestedByPurchaser)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && request.Unit.Project.BuilderId != userId)
                return Forbid();

            var viewModel = new ReviewExtensionRequestViewModel
            {
                RequestId = request.Id,
                UnitId = request.UnitId,
                UnitNumber = request.Unit.UnitNumber,
                ProjectName = request.Unit.Project.Name,
                PurchaserName = $"{request.RequestedByPurchaser.FirstName} {request.RequestedByPurchaser.LastName}",
                OriginalClosingDate = request.OriginalClosingDate ?? request.Unit.ClosingDate ?? DateTime.Today,
                RequestedNewClosingDate = request.RequestedNewClosingDate,
                Reason = request.Reason,
                RequestedDate = request.RequestedDate
            };

            return View(viewModel);
        }

        // POST: /Units/ReviewExtensionRequest
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewExtensionRequest(ReviewExtensionRequestViewModel model)
        {
            var request = await _context.ClosingExtensionRequests
                .Include(r => r.Unit)
                    .ThenInclude(u => u.Project)
                .FirstOrDefaultAsync(r => r.Id == model.RequestId);

            if (request == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && request.Unit.Project.BuilderId != userId)
                return Forbid();

            request.Status = model.Approve ? ClosingExtensionStatus.Approved : ClosingExtensionStatus.Rejected;
            request.ReviewedByBuilderId = userId;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewerNotes = model.ReviewerNotes;

            if (model.Approve)
            {
                request.Unit.ClosingDate = request.RequestedNewClosingDate;
                request.Unit.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Capture values needed in background thread (do not close over EF entities)
                var bgUnitId      = request.UnitId;
                var bgUnitNumber  = request.Unit.UnitNumber;
                var bgProjectName = request.Unit.Project.Name;
                var bgBuilderId   = request.Unit.Project.BuilderId;
                var bgNewDate     = request.RequestedNewClosingDate;

                // Fire-and-forget: recalculate SOA + shortfall in a new DI scope,
                // then notify the builder when done (or on failure).
                _ = Task.Run(async () =>
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var soaSvc      = scope.ServiceProvider.GetRequiredService<ISoaCalculationService>();
                    var sfSvc       = scope.ServiceProvider.GetRequiredService<IShortfallAnalysisService>();
                    var notifySvc   = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    var bgLogger    = scope.ServiceProvider.GetRequiredService<ILogger<UnitsController>>();

                    try
                    {
                        await soaSvc.CalculateSOAAsync(bgUnitId, bgBuilderId, "Builder");
                        await sfSvc.AnalyzeShortfallAsync(bgUnitId);

                        await notifySvc.CreateAsync(
                            userId:     bgBuilderId,
                            title:      "SOA Recalculated",
                            message:    $"SOA for Unit {bgUnitNumber} ({bgProjectName}) has been recalculated following the closing date extension to {bgNewDate:MMM dd, yyyy}.",
                            type:       NotificationType.Success,
                            priority:   NotificationPriority.Normal,
                            actionUrl:  $"/Units/Details/{bgUnitId}",
                            actionText: "View SOA",
                            unitId:     bgUnitId
                        );

                        bgLogger.LogInformation(
                            "Background SOA recalculation complete for unit {UnitId} after extension approval.", bgUnitId);
                    }
                    catch (Exception ex)
                    {
                        bgLogger.LogError(ex,
                            "Background SOA recalculation failed for unit {UnitId} after extension approval.", bgUnitId);
                        try
                        {
                            await notifySvc.CreateAsync(
                                userId:     bgBuilderId,
                                title:      "SOA Recalculation Failed",
                                message:    $"Background SOA recalculation for Unit {bgUnitNumber} ({bgProjectName}) encountered an error. Please recalculate manually from the unit page.",
                                type:       NotificationType.Alert,
                                priority:   NotificationPriority.High,
                                actionUrl:  $"/Units/Details/{bgUnitId}",
                                actionText: "View Unit",
                                unitId:     bgUnitId
                            );
                        }
                        catch { /* swallow: avoid crashing the background thread on notification failure */ }
                    }
                });

                TempData["Success"] = $"Extension approved. Closing date updated to {request.RequestedNewClosingDate:MMM dd, yyyy}. " +
                                      "SOA recalculation is running in the background — you will receive a notification when it is complete.";
            }
            else
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = "Extension request rejected.";
            }

            // Notify purchaser of decision
            var purchaserName = request.RequestedByPurchaserId; // used as ID for notification
            if (model.Approve)
                await _notificationService.NotifyExtensionApprovedAsync(request.UnitId, "", request.RequestedByPurchaserId);
            else
                await _notificationService.NotifyExtensionRejectedAsync(request.UnitId, "", request.RequestedByPurchaserId);

            return RedirectToAction(nameof(Details), new { id = request.UnitId });
        }

        // POST: /Units/ReviewSuggestion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewSuggestion(ReviewSuggestionViewModel model)
        {
            var unit = await _context.Units
                .Include(u => u.Project)
                .Include(u => u.ShortfallAnalysis)
                .FirstOrDefaultAsync(u => u.Id == model.UnitId);

            if (unit == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && unit.Project.BuilderId != userId)
                return Forbid();

            if (unit.ShortfallAnalysis == null)
            {
                TempData["Error"] = "No shortfall analysis found. Calculate SOA first.";
                return RedirectToAction(nameof(Details), new { id = model.UnitId });
            }

            unit.ShortfallAnalysis.DecisionAction = model.Decision;
            unit.ShortfallAnalysis.DecisionByBuilderId = userId;
            unit.ShortfallAnalysis.DecisionAt = DateTime.UtcNow;
            unit.ShortfallAnalysis.BuilderModifiedSuggestion = model.Decision == "Modify"
                ? model.ModifiedSuggestion : null;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Builder {UserId} {Decision} AI suggestion for unit {UnitId}",
                userId, model.Decision, model.UnitId);

            TempData["Success"] = $"AI suggestion {model.Decision.ToLower()}ed and decision recorded.";
            return RedirectToAction(nameof(Details), new { id = model.UnitId });
        }

        #region Bulk Import

        // GET: /Units/BulkImport/5 (ProjectId)
        public async Task<IActionResult> BulkImport(int id)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && project.BuilderId != userId)
                return Forbid();

            var currentUnitCount = await _context.Units.CountAsync(u => u.ProjectId == id);
            var viewModel = new BulkImportViewModel
            {
                ProjectId = id,
                ProjectName = project.Name,
                MaxUnits = project.MaxUnits,
                CurrentUnitCount = currentUnitCount
            };

            return View(viewModel);
        }

        // POST: /Units/BulkImport/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkImport(int id, IFormFile file, bool sendInvitations = true, bool skipDuplicates = true)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && project.BuilderId != userId)
                return Forbid();

            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a CSV file to upload.";
                return RedirectToAction(nameof(BulkImport), new { id });
            }

            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Please upload a valid CSV file.";
                return RedirectToAction(nameof(BulkImport), new { id });
            }

            // Quota enforcement for non-admin
            if (!User.IsInRole("Admin"))
            {
                if (project.MaxUnits == null)
                {
                    TempData["Error"] = "Unit quota has not been assigned for this project. Contact an administrator.";
                    return RedirectToAction(nameof(BulkImport), new { id });
                }
            }

            var errors = new List<string>();
            var successCount = 0;
            var skippedCount = 0;

            try
            {
                using var reader = new StreamReader(file.OpenReadStream());
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HeaderValidated = null,
                    MissingFieldFound = null,
                    TrimOptions = TrimOptions.Trim,
                    BadDataFound = null
                };

                using var csv = new CsvReader(reader, config);
                var records = csv.GetRecords<BulkImportRow>().ToList();

                // Quota enforcement: check total after parsing
                if (!User.IsInRole("Admin") && project.MaxUnits.HasValue)
                {
                    var currentUnits = await _context.Units.CountAsync(u => u.ProjectId == id);
                    var remainingSlots = project.MaxUnits.Value - currentUnits;
                    if (records.Count > remainingSlots)
                    {
                        TempData["Error"] = $"CSV contains {records.Count} units but only {remainingSlots} slots remaining (limit: {project.MaxUnits.Value}, current: {currentUnits}).";
                        return RedirectToAction(nameof(BulkImport), new { id });
                    }
                }

                var rowNumber = 1; // Start at 1 for header

                foreach (var row in records)
                {
                    rowNumber++;
                    try
                    {
                        // Validate required fields
                        if (string.IsNullOrWhiteSpace(row.UnitNumber))
                        {
                            errors.Add($"Row {rowNumber}: Unit number is required.");
                            continue;
                        }

                        if (row.PurchasePrice <= 0)
                        {
                            errors.Add($"Row {rowNumber}: Purchase price must be greater than 0.");
                            continue;
                        }

                        // Check for duplicate unit number
                        var existingUnit = await _context.Units
                            .AnyAsync(u => u.ProjectId == id && u.UnitNumber == row.UnitNumber);

                        if (existingUnit)
                        {
                            if (skipDuplicates)
                            {
                                skippedCount++;
                                continue;
                            }
                            errors.Add($"Row {rowNumber}: Unit {row.UnitNumber} already exists in this project.");
                            continue;
                        }

                        // Create unit with SOA fields
                        var unit = new Unit
                        {
                            ProjectId = id,
                            UnitNumber = row.UnitNumber?.Trim() ?? "",
                            FloorNumber = row.FloorNumber?.Trim(),
                            UnitType = ParseUnitType(row.UnitType),
                            Bedrooms = row.Bedrooms,
                            Bathrooms = row.Bathrooms,
                            SquareFootage = row.SquareFootage,
                            PurchasePrice = row.PurchasePrice,
                            HasParking = ParseBool(row.HasParking),
                            ParkingPrice = row.ParkingPrice,
                            HasLocker = ParseBool(row.HasLocker),
                            LockerPrice = row.LockerPrice,
                            OccupancyDate = row.OccupancyDate ?? project.OccupancyDate,
                            ClosingDate = row.ClosingDate ?? project.ClosingDate,
                            Status = UnitStatus.Pending,
                            CreatedAt = DateTime.UtcNow,

                            // ===== SOA Enhancement Fields =====
                            APSDate = row.APSDate,
                            IsFirstTimeBuyer = ParseBool(row.IsFirstTimeBuyer),
                            IsPrimaryResidence = ParseBool(row.IsPrimaryResidence) || string.IsNullOrEmpty(row.IsPrimaryResidence), // Default true

                            // ===== SOA Adjustment Fields (Priority 6C) =====
                            ActualAnnualLandTax = row.ActualAnnualLandTax,
                            ActualMonthlyMaintenanceFee = row.ActualMonthlyMaintenanceFee
                        };

                        _context.Units.Add(unit);
                        await _context.SaveChangesAsync();

                        // Add purchaser if email provided
                        if (!string.IsNullOrWhiteSpace(row.PurchaserEmail))
                        {
                            var email = row.PurchaserEmail.Trim().ToLower();

                            // Check if user exists
                            var purchaserUser = await _userManager.FindByEmailAsync(email);
                            var isNewUser = false;

                            if (purchaserUser == null)
                            {
                                // Create new purchaser user (without password - they'll set it via invitation)
                                purchaserUser = new ApplicationUser
                                {
                                    UserName = email,
                                    Email = email,
                                    FirstName = row.PurchaserFirstName?.Trim() ?? "Purchaser",
                                    LastName = row.PurchaserLastName?.Trim() ?? row.UnitNumber,
                                    PhoneNumber = row.PurchaserPhone?.Trim(),
                                    UserType = UserType.Purchaser,
                                    CreatedByUserId = userId,
                                    EmailConfirmed = false,
                                    IsActive = true,
                                    CreatedAt = DateTime.UtcNow
                                };

                                var createResult = await _userManager.CreateAsync(purchaserUser);
                                if (createResult.Succeeded)
                                {
                                    await _userManager.AddToRoleAsync(purchaserUser, "Purchaser");
                                    isNewUser = true;
                                }
                                else
                                {
                                    errors.Add($"Row {rowNumber}: Could not create purchaser account - {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
                                    // Continue without purchaser
                                    purchaserUser = null;
                                }
                            }

                            if (purchaserUser != null)
                            {
                                // Check if already linked to this unit
                                var existingLink = await _context.UnitPurchasers
                                    .AnyAsync(up => up.UnitId == unit.Id && up.PurchaserId == purchaserUser.Id);

                                if (!existingLink)
                                {
                                    // Link purchaser to unit
                                    var unitPurchaser = new UnitPurchaser
                                    {
                                        UnitId = unit.Id,
                                        PurchaserId = purchaserUser.Id,
                                        IsPrimaryPurchaser = true,
                                        OwnershipPercentage = 100,
                                        CreatedAt = DateTime.UtcNow
                                    };

                                    _context.UnitPurchasers.Add(unitPurchaser);
                                    await _context.SaveChangesAsync();

                                    // Send invitation email if requested and new user
                                    if (sendInvitations && isNewUser)
                                    {
                                        try
                                        {
                                            var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                                                .Replace("/", "_").Replace("+", "-").TrimEnd('=');

                                            // Store token (you may need to add InvitationToken to ApplicationUser)
                                            purchaserUser.SecurityStamp = token;
                                            await _userManager.UpdateAsync(purchaserUser);

                                            await _emailService.SendPurchaserInvitationAsync(
                                                purchaserUser.Email!,
                                                purchaserUser.FirstName,
                                                project.Name,
                                                unit.UnitNumber,
                                                token
                                            );
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogWarning(ex, "Failed to send invitation email to {Email}", purchaserUser.Email);
                                        }
                                    }
                                }
                            }
                        }

                        // Add deposits if provided
                        await AddDepositsFromImport(unit.Id, row);

                        await _context.SaveChangesAsync();
                        successCount++;

                        _logger.LogInformation("Bulk import: Unit {UnitNumber} created for Project {ProjectId}",
                            row.UnitNumber, id);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Row {rowNumber}: {ex.Message}");
                        _logger.LogError(ex, "Error importing row {RowNumber}", rowNumber);
                    }
                }

                // Build success message
                var message = $"Successfully imported {successCount} unit(s).";
                if (skippedCount > 0)
                    message += $" Skipped {skippedCount} duplicate(s).";

                if (successCount > 0)
                {
                    TempData["Success"] = message;
                }

                if (errors.Any())
                {
                    TempData["ImportErrors"] = errors.Take(20).ToList(); // Limit to 20 errors
                    if (errors.Count > 20)
                    {
                        ((List<string>)TempData["ImportErrors"]).Add($"... and {errors.Count - 20} more errors.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing CSV file for Project {ProjectId}", id);
                TempData["Error"] = $"Error processing CSV file: {ex.Message}";
            }

            return RedirectToAction("Dashboard", "Projects", new { id });
        }

        // GET: /Units/DownloadImportTemplate/5
        public IActionResult DownloadImportTemplate(int id)
        {
            var csv = new StringBuilder();

            // Header row - Updated for SOA Compliance
            csv.AppendLine(string.Join(",", new[]
            {
        // Unit Info
        "UnitNumber", "FloorNumber", "UnitType", "Bedrooms", "Bathrooms", "SquareFootage", "PurchasePrice",
        // Parking & Locker
        "HasParking", "ParkingPrice", "HasLocker", "LockerPrice",
        // Dates
        "OccupancyDate", "ClosingDate", "APSDate",
        // SOA Fields
        "IsFirstTimeBuyer", "IsPrimaryResidence",
        // SOA Adjustment Fields
        "ActualAnnualLandTax", "ActualMonthlyMaintenanceFee",
        // Purchaser Info
        "PurchaserEmail", "PurchaserFirstName", "PurchaserLastName", "PurchaserPhone",
        // Deposit 1
        "Deposit1Amount", "Deposit1DueDate", "Deposit1PaidDate", "Deposit1Holder", "Deposit1InterestEligible", "Deposit1InterestRate",
        // Deposit 2
        "Deposit2Amount", "Deposit2DueDate", "Deposit2PaidDate", "Deposit2Holder", "Deposit2InterestEligible", "Deposit2InterestRate",
        // Deposit 3
        "Deposit3Amount", "Deposit3DueDate", "Deposit3PaidDate", "Deposit3Holder", "Deposit3InterestEligible", "Deposit3InterestRate",
        // Deposit 4
        "Deposit4Amount", "Deposit4DueDate", "Deposit4PaidDate", "Deposit4Holder", "Deposit4InterestEligible", "Deposit4InterestRate",
        // Deposit 5
        "Deposit5Amount", "Deposit5DueDate", "Deposit5PaidDate", "Deposit5Holder", "Deposit5InterestEligible", "Deposit5InterestRate"
    }));

            // Sample data row 1 - First-time buyer, primary residence, with actual tax/maintenance
            csv.AppendLine("101,1,OneBedroom,1,1,650,599000,true,50000,true,5000,2026-06-01,2026-09-01,2024-01-10,true,true,5200,450,john.smith@email.com,John,Smith,416-555-1234,29950,2024-01-15,2024-01-15,Trust,true,0.02,29950,2024-04-15,2024-04-15,Trust,true,0.02,29950,2024-07-15,,Trust,true,0.02,0,,,,,,0,,,,,");

            // Sample data row 2 - Not first-time buyer, with actual tax only
            csv.AppendLine("102,1,TwoBedroom,2,2,850,699000,true,50000,false,0,2026-06-01,2026-09-01,2024-02-01,false,true,6100,,jane.doe@email.com,Jane,Doe,416-555-5678,34950,2024-02-15,2024-02-15,Builder,false,,34950,2024-05-15,,,Builder,false,,0,,,,,,0,,,,,,0,,,,,");

            // Sample data row 3 - No purchaser yet, no tax/maintenance data
            csv.AppendLine("201,2,Studio,0,1,450,399000,false,0,false,0,2026-06-01,2026-09-01,,true,true,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,");

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", "PreConHub_BulkImport_Template_v2.csv");
        }

        private async Task AddDepositsFromImport(int unitId, BulkImportRow row)
        {
            // Deposit 1
            if (row.Deposit1Amount > 0)
            {
                _context.Deposits.Add(CreateDepositFromImport(
                    unitId, "Deposit 1",
                    row.Deposit1Amount,
                    row.Deposit1DueDate,
                    row.Deposit1PaidDate,
                    row.Deposit1Holder,
                    row.Deposit1InterestEligible,
                    row.Deposit1InterestRate
                ));
            }

            // Deposit 2
            if (row.Deposit2Amount > 0)
            {
                _context.Deposits.Add(CreateDepositFromImport(
                    unitId, "Deposit 2",
                    row.Deposit2Amount,
                    row.Deposit2DueDate ?? DateTime.UtcNow.AddDays(30),
                    row.Deposit2PaidDate,
                    row.Deposit2Holder,
                    row.Deposit2InterestEligible,
                    row.Deposit2InterestRate
                ));
            }

            // Deposit 3
            if (row.Deposit3Amount > 0)
            {
                _context.Deposits.Add(CreateDepositFromImport(
                    unitId, "Deposit 3",
                    row.Deposit3Amount,
                    row.Deposit3DueDate ?? DateTime.UtcNow.AddDays(60),
                    row.Deposit3PaidDate,
                    row.Deposit3Holder,
                    row.Deposit3InterestEligible,
                    row.Deposit3InterestRate
                ));
            }

            // Deposit 4
            if (row.Deposit4Amount > 0)
            {
                _context.Deposits.Add(CreateDepositFromImport(
                    unitId, "Deposit 4",
                    row.Deposit4Amount,
                    row.Deposit4DueDate ?? DateTime.UtcNow.AddDays(90),
                    row.Deposit4PaidDate,
                    row.Deposit4Holder,
                    row.Deposit4InterestEligible,
                    row.Deposit4InterestRate
                ));
            }

            // Deposit 5
            if (row.Deposit5Amount > 0)
            {
                _context.Deposits.Add(CreateDepositFromImport(
                    unitId, "Deposit 5",
                    row.Deposit5Amount,
                    row.Deposit5DueDate ?? DateTime.UtcNow.AddDays(120),
                    row.Deposit5PaidDate,
                    row.Deposit5Holder,
                    row.Deposit5InterestEligible,
                    row.Deposit5InterestRate
                ));
            }
        }

        /// <summary>
        /// Helper method to create a deposit with SOA-compliant fields
        /// </summary>
        private Deposit CreateDepositFromImport(
            int unitId,
            string depositName,
            decimal amount,
            DateTime? dueDate,
            DateTime? paidDate,
            string? holder,
            string? interestEligible,
            decimal? interestRate)
        {
            return new Deposit
            {
                UnitId = unitId,
                DepositName = depositName,
                Amount = amount,
                DueDate = dueDate ?? DateTime.UtcNow,
                IsPaid = paidDate.HasValue,
                PaidDate = paidDate,
                Status = paidDate.HasValue ? DepositStatus.Paid : DepositStatus.Pending,
                CreatedAt = DateTime.UtcNow,

                // SOA Enhancement Fields
                Holder = ParseDepositHolder(holder),
                IsInterestEligible = ParseBool(interestEligible),
                InterestRate = interestRate,
                CompoundingType = Models.Entities.InterestCompoundingType.Simple // Default
            };
        }

        /// <summary>
        /// Parse deposit holder from string
        /// </summary>
        private Models.Entities.DepositHolder ParseDepositHolder(string? holder)
        {
            if (string.IsNullOrWhiteSpace(holder))
                return Models.Entities.DepositHolder.Builder; // Default

            return holder.ToLower().Trim() switch
            {
                "trust" => Models.Entities.DepositHolder.Trust,
                "lawyer" => Models.Entities.DepositHolder.Lawyer,
                _ => Models.Entities.DepositHolder.Builder
            };
        }

        private UnitType ParseUnitType(string? unitType)
        {
            if (string.IsNullOrWhiteSpace(unitType))
                return UnitType.Other;

            var normalized = unitType.ToLower().Trim()
                .Replace(" ", "")
                .Replace("+", "plus")
                .Replace("-", "");

            return normalized switch
            {
                "studio" => UnitType.Studio,
                "onebedroom" or "1bedroom" or "1br" or "1bed" => UnitType.OneBedroom,
                "oneplusden" or "1plusden" or "1den" or "1bedplusden" => UnitType.OnePlusDen,
                "twobedroom" or "2bedroom" or "2br" or "2bed" => UnitType.TwoBedroom,
                "twoplusden" or "2plusden" or "2den" or "2bedplusden" => UnitType.TwoPlusDen,
                "threebedroom" or "3bedroom" or "3br" or "3bed" => UnitType.ThreeBedroom,
                "penthouse" or "ph" => UnitType.Penthouse,
                "townhouse" or "th" => UnitType.Townhouse,
                _ => UnitType.Other
            };
        }

        private bool ParseBool(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var normalized = value.ToLower().Trim();
            return normalized == "true" || normalized == "yes" || normalized == "1" || normalized == "y";
        }

        #endregion

        #region APS Document Analysis

        // GET: /Units/UploadAps/5 (ProjectId)
        public async Task<IActionResult> UploadAps(int id)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && project.BuilderId != userId)
                return Forbid();

            var viewModel = new UploadApsViewModel
            {
                ProjectId = id,
                ProjectName = project.Name
            };

            return View(viewModel);
        }

        // POST: /Units/UploadAps/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(10_000_000)] // 10MB limit
        public async Task<IActionResult> UploadAps(int id, IFormFile apsFile)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && project.BuilderId != userId)
                return Forbid();

            if (apsFile == null || apsFile.Length == 0)
            {
                TempData["Error"] = "Please select a PDF file to upload.";
                return RedirectToAction(nameof(UploadAps), new { id });
            }

            if (!apsFile.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Please upload a valid PDF file.";
                return RedirectToAction(nameof(UploadAps), new { id });
            }

            try
            {
                // Process the APS document
                using var stream = apsFile.OpenReadStream();
                var extractedData = await _documentAnalysisService.ProcessApsUploadAsync(stream);

                // Store extracted data in TempData for review
                var viewModel = new ReviewApsDataViewModel
                {
                    ProjectId = id,
                    ProjectName = project.Name,
                    FileName = apsFile.FileName,
                    ExtractedData = extractedData
                };

                // Store in session for the review step
                HttpContext.Session.SetString($"ApsData_{id}",
                    System.Text.Json.JsonSerializer.Serialize(extractedData));

                return View("ReviewApsData", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing APS document for Project {ProjectId}", id);
                TempData["Error"] = $"Error processing document: {ex.Message}";
                return RedirectToAction(nameof(UploadAps), new { id });
            }
        }

        // POST: /Units/ConfirmApsData/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmApsData(int id, ReviewApsDataViewModel model)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && project.BuilderId != userId)
                return Forbid();

            try
            {
                // Check if unit already exists
                var existingUnit = await _context.Units
                    .AnyAsync(u => u.ProjectId == id && u.UnitNumber == model.UnitNumber);

                if (existingUnit)
                {
                    TempData["Error"] = $"Unit {model.UnitNumber} already exists in this project.";
                    return RedirectToAction(nameof(UploadAps), new { id });
                }

                // Create Unit
                var unit = new Unit
                {
                    ProjectId = id,
                    UnitNumber = model.UnitNumber ?? "Unknown",
                    FloorNumber = model.FloorNumber,
                    UnitType = Enum.TryParse<UnitType>(model.UnitType, out var unitType) ? unitType : UnitType.Other,
                    Bedrooms = model.Bedrooms ?? 0,
                    Bathrooms = model.Bathrooms ?? 0,
                    SquareFootage = model.SquareFootage ?? 0,
                    PurchasePrice = model.PurchasePrice ?? 0,
                    HasParking = model.HasParking,
                    ParkingPrice = model.ParkingPrice ?? 0,
                    HasLocker = model.HasLocker,
                    LockerPrice = model.LockerPrice ?? 0,
                    OccupancyDate = model.OccupancyDate ?? project.OccupancyDate,
                    ClosingDate = model.ClosingDate ?? project.ClosingDate,
                    Status = UnitStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Units.Add(unit);
                await _context.SaveChangesAsync();

                // Create Purchaser(s)
                if (!string.IsNullOrWhiteSpace(model.PurchaserEmail))
                {
                    var email = model.PurchaserEmail.Trim().ToLower();
                    var purchaserUser = await _userManager.FindByEmailAsync(email);

                    if (purchaserUser == null)
                    {
                        purchaserUser = new ApplicationUser
                        {
                            UserName = email,
                            Email = email,
                            FirstName = model.PurchaserFirstName ?? "Purchaser",
                            LastName = model.PurchaserLastName ?? model.UnitNumber ?? "",
                            PhoneNumber = model.PurchaserPhone,
                            UserType = UserType.Purchaser,
                            CreatedByUserId = userId,
                            EmailConfirmed = false,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };

                        var createResult = await _userManager.CreateAsync(purchaserUser);
                        if (createResult.Succeeded)
                        {
                            await _userManager.AddToRoleAsync(purchaserUser, "Purchaser");
                        }
                    }

                    if (purchaserUser != null)
                    {
                        var unitPurchaser = new UnitPurchaser
                        {
                            UnitId = unit.Id,
                            PurchaserId = purchaserUser.Id,
                            IsPrimaryPurchaser = true,
                            OwnershipPercentage = 100,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.UnitPurchasers.Add(unitPurchaser);
                    }
                }

                // Create Co-Purchaser if provided
                if (!string.IsNullOrWhiteSpace(model.CoPurchaserEmail))
                {
                    var coEmail = model.CoPurchaserEmail.Trim().ToLower();
                    var coPurchaserUser = await _userManager.FindByEmailAsync(coEmail);

                    if (coPurchaserUser == null)
                    {
                        coPurchaserUser = new ApplicationUser
                        {
                            UserName = coEmail,
                            Email = coEmail,
                            FirstName = model.CoPurchaserFirstName ?? "Co-Purchaser",
                            LastName = model.CoPurchaserLastName ?? "",
                            PhoneNumber = model.CoPurchaserPhone,
                            UserType = UserType.Purchaser,
                            CreatedByUserId = userId,
                            EmailConfirmed = false,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };

                        var createResult = await _userManager.CreateAsync(coPurchaserUser);
                        if (createResult.Succeeded)
                        {
                            await _userManager.AddToRoleAsync(coPurchaserUser, "Purchaser");
                        }
                    }

                    if (coPurchaserUser != null)
                    {
                        var coUnitPurchaser = new UnitPurchaser
                        {
                            UnitId = unit.Id,
                            PurchaserId = coPurchaserUser.Id,
                            IsPrimaryPurchaser = false,
                            OwnershipPercentage = 50, // Adjust as needed
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.UnitPurchasers.Add(coUnitPurchaser);
                    }
                }

                // Create Deposits
                if (model.Deposit1Amount > 0)
                {
                    _context.Deposits.Add(new Deposit
                    {
                        UnitId = unit.Id,
                        DepositName = "Initial Deposit",
                        Amount = model.Deposit1Amount,
                        DueDate = model.Deposit1DueDate ?? DateTime.UtcNow,
                        IsPaid = model.Deposit1Paid,
                        PaidDate = model.Deposit1Paid ? model.Deposit1DueDate : null,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                if (model.Deposit2Amount > 0)
                {
                    _context.Deposits.Add(new Deposit
                    {
                        UnitId = unit.Id,
                        DepositName = "Second Deposit",
                        Amount = model.Deposit2Amount,
                        DueDate = model.Deposit2DueDate ?? DateTime.UtcNow.AddDays(30),
                        IsPaid = model.Deposit2Paid,
                        PaidDate = model.Deposit2Paid ? model.Deposit2DueDate : null,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                if (model.Deposit3Amount > 0)
                {
                    _context.Deposits.Add(new Deposit
                    {
                        UnitId = unit.Id,
                        DepositName = "Third Deposit",
                        Amount = model.Deposit3Amount,
                        DueDate = model.Deposit3DueDate ?? DateTime.UtcNow.AddDays(60),
                        IsPaid = model.Deposit3Paid,
                        PaidDate = model.Deposit3Paid ? model.Deposit3DueDate : null,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                if (model.Deposit4Amount > 0)
                {
                    _context.Deposits.Add(new Deposit
                    {
                        UnitId = unit.Id,
                        DepositName = "Fourth Deposit",
                        Amount = model.Deposit4Amount,
                        DueDate = model.Deposit4DueDate ?? DateTime.UtcNow.AddDays(90),
                        IsPaid = model.Deposit4Paid,
                        PaidDate = model.Deposit4Paid ? model.Deposit4DueDate : null,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                if (model.Deposit5Amount > 0)
                {
                    _context.Deposits.Add(new Deposit
                    {
                        UnitId = unit.Id,
                        DepositName = "Balance Deposit",
                        Amount = model.Deposit5Amount,
                        DueDate = model.Deposit5DueDate ?? DateTime.UtcNow.AddDays(120),
                        IsPaid = model.Deposit5Paid,
                        PaidDate = model.Deposit5Paid ? model.Deposit5DueDate : null,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                await _context.SaveChangesAsync();

                // Calculate SOA
                try
                {
                    await _soaService.CalculateSOAAsync(unit.Id);
                    await _shortfallService.AnalyzeShortfallAsync(unit.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error calculating SOA for newly created unit {UnitId}", unit.Id);
                }

                _logger.LogInformation("Unit {UnitNumber} created from APS analysis for Project {ProjectId}",
                    unit.UnitNumber, id);

                TempData["Success"] = $"Unit {unit.UnitNumber} created successfully from APS document!";

                // Clear session data
                HttpContext.Session.Remove($"ApsData_{id}");

                return RedirectToAction("Details", new { id = unit.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating unit from APS data for Project {ProjectId}", id);
                TempData["Error"] = $"Error creating unit: {ex.Message}";
                return RedirectToAction(nameof(UploadAps), new { id });
            }
        }

        #endregion

        #region SOA Version History

        // GET: /Units/SOAVersionHistory/5 (UnitId)
        public async Task<IActionResult> SOAVersionHistory(int id)
        {
            var unit = await _context.Units
                .Include(u => u.Project)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (unit == null)
                return NotFound();

            // Verify access: builder owns project or admin
            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin") && unit.Project.BuilderId != userId)
                return Forbid();

            var versions = await _context.SOAVersions
                .Where(v => v.UnitId == id)
                .Include(v => v.CreatedByUser)
                .OrderByDescending(v => v.VersionNumber)
                .ToListAsync();

            var vm = new SOAVersionHistoryViewModel
            {
                UnitId = id,
                UnitNumber = unit.UnitNumber,
                ProjectName = unit.Project.Name,
                Versions = versions.Select(v => new SOAVersionItem
                {
                    Id = v.Id,
                    VersionNumber = v.VersionNumber,
                    Source = v.Source.ToString(),
                    SourceBadgeClass = v.Source == SOAVersionSource.SystemCalculation ? "bg-info" :
                                       v.Source == SOAVersionSource.LawyerUpload ? "bg-primary" : "bg-warning",
                    BalanceDueOnClosing = v.BalanceDueOnClosing,
                    TotalVendorCredits = v.TotalVendorCredits,
                    TotalPurchaserCredits = v.TotalPurchaserCredits,
                    CashRequiredToClose = v.CashRequiredToClose,
                    UploadedFilePath = v.UploadedFilePath,
                    CreatedByName = $"{v.CreatedByUser.FirstName} {v.CreatedByUser.LastName}".Trim(),
                    CreatedByRole = v.CreatedByRole,
                    CreatedAt = v.CreatedAt,
                    Notes = v.Notes
                }).ToList()
            };

            return View(vm);
        }

        #endregion

        #region SOA Confirmation

        // POST: /Units/ConfirmSOA/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmSOA(int id)
        {
            var userId = _userManager.GetUserId(User);
            var unit = await _context.Units
                .Include(u => u.SOA)
                .Include(u => u.Project)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (unit == null) return NotFound();

            // Verify builder owns this project (or admin)
            if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin") && unit.Project.BuilderId != userId)
                return Forbid();

            if (unit.SOA == null)
            {
                TempData["Error"] = "No SOA exists for this unit. Calculate or upload SOA first.";
                return RedirectToAction("Details", new { id });
            }

            if (unit.SOA.IsConfirmedByBuilder)
            {
                TempData["Info"] = "SOA is already confirmed by builder.";
                return RedirectToAction("Details", new { id });
            }

            // Set builder confirmation
            unit.SOA.IsConfirmedByBuilder = true;
            unit.SOA.BuilderConfirmedAt = DateTime.UtcNow;
            unit.SOA.ConfirmedByBuilderId = userId;

            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "SOA",
                EntityId = id,
                Action = "ConfirmSOA",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = User.IsInRole("Admin") ? "Admin" : "Builder",
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            // Attempt auto-lock if both parties have confirmed
            try
            {
                var locked = await _soaService.LockSOAAsync(id, userId!);
                if (locked)
                {
                    TempData["Success"] = $"SOA confirmed and locked for Unit {unit.UnitNumber}. Both builder and lawyer have confirmed.";
                }
                else
                {
                    TempData["Success"] = $"SOA confirmed by builder for Unit {unit.UnitNumber}. Awaiting lawyer confirmation to lock.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SOA auto-lock not triggered for Unit {UnitId}", id);
                TempData["Success"] = $"SOA confirmed by builder for Unit {unit.UnitNumber}. Awaiting lawyer confirmation to lock.";
            }

            return RedirectToAction("Details", new { id });
        }

        #endregion

    }
}
