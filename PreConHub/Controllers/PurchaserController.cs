using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
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
    public class PurchaserController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ISoaCalculationService _soaService;
        private readonly IShortfallAnalysisService _shortfallService;
        private readonly ILogger<PurchaserController> _logger;
        private readonly IPdfService _pdfService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly INotificationService _notificationService;

        public PurchaserController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ISoaCalculationService soaService,
            IShortfallAnalysisService shortfallService,
            IPdfService pdfService,
            IWebHostEnvironment webHostEnvironment,
            INotificationService notificationService,
            ILogger<PurchaserController> logger)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _soaService = soaService;
            _shortfallService = shortfallService;
            _pdfService = pdfService;
            _webHostEnvironment = webHostEnvironment;
            _notificationService = notificationService;
            _logger = logger;
        }

        // GET: /Purchaser/AcceptInvitation?email=xxx&code=xxx
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

            // Check if user already has a confirmed email (already activated)
            if (user.EmailConfirmed)
            {
                TempData["Info"] = "Your account is already activated. Please log in.";
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            // Get the unit info to show on the welcome page
            var unitPurchaser = await _context.UnitPurchasers
                .Include(up => up.Unit)
                    .ThenInclude(u => u.Project)
                .FirstOrDefaultAsync(up => up.PurchaserId == user.Id);

            var viewModel = new AcceptInvitationViewModel
            {
                Email = email,
                Code = code,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UnitNumber = unitPurchaser?.Unit?.UnitNumber,
                ProjectName = unitPurchaser?.Unit?.Project?.Name
            };

            return View(viewModel);
        }

        // POST: /Purchaser/AcceptInvitation
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptInvitation(AcceptInvitationViewModel model)
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

            // Verify the token and reset password
            // Token is already decoded by ASP.NET - don't decode again!
            var result = await _userManager.ResetPasswordAsync(user, model.Code, model.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    // Check if it's an expired/invalid token
                    if (error.Code == "InvalidToken")
                    {
                        ModelState.AddModelError("", "This invitation link has expired or is invalid. Please contact your builder for a new invitation.");
                    }
                    else
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                }
                return View(model);
            }

            // Confirm email
            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);

            // Sign in the user automatically
            await _signInManager.SignInAsync(user, isPersistent: false);

            _logger.LogInformation("Purchaser {Email} accepted invitation and activated account", model.Email);

            TempData["Success"] = "Welcome! Your account has been activated successfully.";
            return RedirectToAction(nameof(Dashboard));
        }

        // GET: /Purchaser/InvalidInvitation
        [AllowAnonymous]
        public IActionResult InvalidInvitation()
        {
            return View();
        }

        // GET: /Purchaser/Dashboard
        [Authorize(Roles = "Purchaser")]
        public async Task<IActionResult> Dashboard()
        {
            var userId = _userManager.GetUserId(User);
            
            var unitPurchasers = await _context.UnitPurchasers
                .Include(up => up.Unit)
                    .ThenInclude(u => u.Project)
                .Include(up => up.Unit)
                    .ThenInclude(u => u.Deposits)
                .Include(up => up.Unit)
                    .ThenInclude(u => u.SOA)
                .Include(up => up.Unit)
                    .ThenInclude(u => u.ShortfallAnalysis)
                .Include(up => up.Unit)
                    .ThenInclude(u => u.Documents)
                .Include(up => up.Unit)
                    .ThenInclude(u => u.ExtensionRequests)
                .Include(up => up.MortgageInfo)
                .Include(up => up.Financials)
                .Where(up => up.PurchaserId == userId)
                .ToListAsync();

            if (!unitPurchasers.Any())
            {
                return View("NoUnits");
            }

            var viewModel = new PurchaserDashboardViewModel
            {
                PurchaserName = User.Identity?.Name ?? "Purchaser",
                Units = unitPurchasers.Select(up => {
                    // Get documents uploaded by this purchaser for this unit
                    var purchaserDocuments = up.Unit.Documents
                        .Where(d => d.UploadedById == userId && d.Source == DocumentSource.Purchaser)
                        .ToList();

                    return new PurchaserUnitViewModel
                    {
                    UnitPurchaserId = up.Id,
                    UnitId = up.UnitId,
                    UnitNumber = up.Unit.UnitNumber,
                    ProjectName = up.Unit.Project.Name,
                    ProjectAddress = $"{up.Unit.Project.Address}, {up.Unit.Project.City}",
                    PurchasePrice = up.Unit.PurchasePrice,
                    ClosingDate = up.Unit.ClosingDate,
                    Status = up.Unit.Status,

                    // Mortgage Info
                    HasMortgageInfo = up.MortgageInfo != null,
                    MortgageApproved = up.MortgageInfo?.HasMortgageApproval ?? false,
                    MortgageAmount = up.MortgageInfo?.ApprovedAmount ?? 0,
                    MortgageProvider = up.MortgageInfo?.MortgageProvider,

                    // Financials
                    HasFinancialsSubmitted = up.Financials != null,
                    AdditionalCashAvailable = up.Financials?.AdditionalCashAvailable ?? 0,

                    // Deposits
                    TotalDeposits = up.Unit.Deposits.Sum(d => d.Amount),
                    DepositsPaid = up.Unit.Deposits.Where(d => d.IsPaid).Sum(d => d.Amount),

                    // SOA
                    HasSOA = up.Unit.SOA != null,
                    BalanceDueOnClosing = up.Unit.SOA?.BalanceDueOnClosing ?? 0,
                    CashRequiredToClose = up.Unit.SOA?.CashRequiredToClose ?? 0,

                    // Shortfall
                    ShortfallAmount = up.Unit.ShortfallAnalysis?.ShortfallAmount ?? 0,
                    ShortfallPercentage = up.Unit.ShortfallAnalysis?.ShortfallPercentage ?? 0,
                    Recommendation = up.Unit.Recommendation,

                    // Documents
                    HasDocumentsUploaded = purchaserDocuments.Any(),
                    DocumentsUploadedCount = purchaserDocuments.Count,
                    RequiredDocumentsCount = 3, // Mortgage approval, ID, Bank statement

                    // Extension Requests
                    ExtensionRequests = up.Unit.ExtensionRequests
                        .OrderByDescending(er => er.RequestedDate)
                        .Select(er => new ExtensionRequestItem
                        {
                            RequestId = er.Id,
                            UnitId = er.UnitId,
                            UnitNumber = up.Unit.UnitNumber,
                            ProjectName = up.Unit.Project.Name,
                            OriginalClosingDate = er.OriginalClosingDate,
                            RequestedNewClosingDate = er.RequestedNewClosingDate,
                            Reason = er.Reason,
                            RequestedDate = er.RequestedDate,
                            Status = er.Status,
                            ReviewerNotes = er.ReviewerNotes,
                            ReviewedAt = er.ReviewedAt
                        }).ToList()
                };
                }).ToList()
            };

            // Calculate completion status
            foreach (var unit in viewModel.Units)
            {
                unit.CompletionSteps = new List<CompletionStep>
                {
                    new CompletionStep { Name = "Account Created", IsComplete = true },
                    new CompletionStep { Name = "Mortgage Info", IsComplete = unit.HasMortgageInfo },
                    new CompletionStep { Name = "Financial Info", IsComplete = unit.HasFinancialsSubmitted },
                    new CompletionStep { Name = "Documents Uploaded", IsComplete = unit.HasDocumentsUploaded },
                    new CompletionStep { Name = "Ready for Review", IsComplete = unit.HasMortgageInfo && unit.HasFinancialsSubmitted }
                };
                unit.CompletionPercentage = (int)((unit.CompletionSteps.Count(s => s.IsComplete) / (decimal)unit.CompletionSteps.Count) * 100);
            }

            return View(viewModel);
        }

        // GET: /Purchaser/Unit/5
        [Authorize(Roles = "Purchaser")]
        public async Task<IActionResult> Unit(int id)
        {
            var userId = _userManager.GetUserId(User);
            
            var unitPurchaser = await _context.UnitPurchasers
                .Include(up => up.Unit)
                    .ThenInclude(u => u.Project)
                .Include(up => up.Unit)
                    .ThenInclude(u => u.Deposits)
                .Include(up => up.Unit)
                    .ThenInclude(u => u.SOA)
                .Include(up => up.Unit)
                    .ThenInclude(u => u.ShortfallAnalysis)
                .Include(up => up.MortgageInfo)
                .Include(up => up.Financials)
                .FirstOrDefaultAsync(up => up.UnitId == id && up.PurchaserId == userId);

            if (unitPurchaser == null)
            {
                return NotFound();
            }

            return View(unitPurchaser);
        }

        // GET: /Purchaser/SubmitMortgageInfo/5 (UnitId)
        [Authorize(Roles = "Purchaser")]
        public async Task<IActionResult> SubmitMortgageInfo(int id)
        {
            var userId = _userManager.GetUserId(User);
            
            var unitPurchaser = await _context.UnitPurchasers
                .Include(up => up.Unit)
                    .ThenInclude(u => u.Project)
                .Include(up => up.MortgageInfo)
                .FirstOrDefaultAsync(up => up.UnitId == id && up.PurchaserId == userId);

            if (unitPurchaser == null)
            {
                return NotFound();
            }

            var viewModel = new SubmitMortgageInfoViewModel
            {
                UnitId = id,
                UnitNumber = unitPurchaser.Unit.UnitNumber,
                ProjectName = unitPurchaser.Unit.Project.Name,
                PurchasePrice = unitPurchaser.Unit.PurchasePrice
            };

            // Pre-fill if mortgage info exists
            if (unitPurchaser.MortgageInfo != null)
            {
                viewModel.HasMortgageApproval = unitPurchaser.MortgageInfo.HasMortgageApproval;
                viewModel.ApprovalType = unitPurchaser.MortgageInfo.ApprovalType;
                viewModel.MortgageProvider = unitPurchaser.MortgageInfo.MortgageProvider;
                viewModel.ApprovedAmount = unitPurchaser.MortgageInfo.ApprovedAmount;
                viewModel.InterestRate = unitPurchaser.MortgageInfo.InterestRate;
                viewModel.AmortizationYears = unitPurchaser.MortgageInfo.AmortizationYears;
                viewModel.ApprovalExpiryDate = unitPurchaser.MortgageInfo.ApprovalExpiryDate;
                viewModel.HasConditions = unitPurchaser.MortgageInfo.HasConditions;
                viewModel.Conditions = unitPurchaser.MortgageInfo.Conditions;
                viewModel.IsBlanketMortgage = unitPurchaser.MortgageInfo.IsBlanketMortgage;
                viewModel.PurchaserAppraisalValue = unitPurchaser.MortgageInfo.PurchaserAppraisalValue;
                viewModel.EstimatedFundingDate = unitPurchaser.MortgageInfo.EstimatedFundingDate;
                viewModel.CreditScore = unitPurchaser.MortgageInfo.CreditScore;
                viewModel.CreditBureau = unitPurchaser.MortgageInfo.CreditBureau;
                viewModel.Comments = unitPurchaser.MortgageInfo.Comments;
            }

            return View(viewModel);
        }

        // POST: /Purchaser/SubmitMortgageInfo
        [HttpPost]
        [Authorize(Roles = "Purchaser")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitMortgageInfo(SubmitMortgageInfoViewModel model)
        {
            var userId = _userManager.GetUserId(User);
            
            var unitPurchaser = await _context.UnitPurchasers
                .Include(up => up.Unit)
                .Include(up => up.MortgageInfo)
                .FirstOrDefaultAsync(up => up.UnitId == model.UnitId && up.PurchaserId == userId);

            if (unitPurchaser == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Create or update mortgage info
            if (unitPurchaser.MortgageInfo == null)
            {
                unitPurchaser.MortgageInfo = new MortgageInfo
                {
                    UnitPurchaserId = unitPurchaser.Id,
                    CreatedAt = DateTime.UtcNow
                };
                _context.MortgageInfos.Add(unitPurchaser.MortgageInfo);
            }

            unitPurchaser.MortgageInfo.HasMortgageApproval = model.HasMortgageApproval;
            unitPurchaser.MortgageInfo.ApprovalType = model.ApprovalType;
            unitPurchaser.MortgageInfo.MortgageProvider = model.MortgageProvider;
            unitPurchaser.MortgageInfo.ApprovedAmount = model.ApprovedAmount;
            unitPurchaser.MortgageInfo.InterestRate = model.InterestRate;
            unitPurchaser.MortgageInfo.AmortizationYears = model.AmortizationYears;
            unitPurchaser.MortgageInfo.ApprovalExpiryDate = model.ApprovalExpiryDate;
            unitPurchaser.MortgageInfo.HasConditions = model.HasConditions;
            unitPurchaser.MortgageInfo.Conditions = model.Conditions;
            unitPurchaser.MortgageInfo.IsBlanketMortgage = model.IsBlanketMortgage;
            unitPurchaser.MortgageInfo.PurchaserAppraisalValue = model.PurchaserAppraisalValue;
            unitPurchaser.MortgageInfo.EstimatedFundingDate = model.EstimatedFundingDate;
            unitPurchaser.MortgageInfo.CreditScore = model.CreditScore;
            unitPurchaser.MortgageInfo.CreditBureau = model.CreditBureau;
            unitPurchaser.MortgageInfo.Comments = model.Comments;
            unitPurchaser.MortgageInfo.UpdatedAt = DateTime.UtcNow;

            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "MortgageInfo",
                EntityId = model.UnitId,
                Action = "Submit",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = "Purchaser",
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { model.HasMortgageApproval, model.ApprovalType, model.ApprovedAmount }),
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            // Recalculate SOA and shortfall
            try
            {
                await _soaService.CalculateSOAAsync(model.UnitId);
                await _shortfallService.AnalyzeShortfallAsync(model.UnitId);

                var user = await _userManager.GetUserAsync(User);
                var purchaserName = $"{user?.FirstName} {user?.LastName}".Trim();

                await _notificationService.NotifyMortgageInfoSubmittedAsync(
                    model.UnitId,
                    purchaserName
                );

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recalculating SOA for unit {UnitId}", model.UnitId);
            }

            _logger.LogInformation("Purchaser {UserId} submitted mortgage info for unit {UnitId}", userId, model.UnitId);

            TempData["Success"] = "Mortgage information saved successfully!";
            return RedirectToAction(nameof(Dashboard));
        }

        // GET: /Purchaser/SubmitFinancials/5 (UnitId)
        [Authorize(Roles = "Purchaser")]
        public async Task<IActionResult> SubmitFinancials(int id)
        {
            var userId = _userManager.GetUserId(User);
            
            var unitPurchaser = await _context.UnitPurchasers
                .Include(up => up.Unit)
                    .ThenInclude(u => u.Project)
                .Include(up => up.Financials)
                .FirstOrDefaultAsync(up => up.UnitId == id && up.PurchaserId == userId);

            if (unitPurchaser == null)
            {
                return NotFound();
            }

            var viewModel = new SubmitFinancialsViewModel
            {
                UnitId = id,
                UnitNumber = unitPurchaser.Unit.UnitNumber,
                ProjectName = unitPurchaser.Unit.Project.Name
            };

            // Pre-fill if financials exist
            if (unitPurchaser.Financials != null)
            {
                viewModel.AdditionalCashAvailable = unitPurchaser.Financials.AdditionalCashAvailable;
                viewModel.RRSPAvailable = unitPurchaser.Financials.RRSPAvailable;
                viewModel.GiftFromFamily = unitPurchaser.Financials.GiftFromFamily;
                viewModel.ProceedsFromSale = unitPurchaser.Financials.ProceedsFromSale;
                viewModel.OtherFundsDescription = unitPurchaser.Financials.OtherFundsDescription;
                viewModel.OtherFundsAmount = unitPurchaser.Financials.OtherFundsAmount;
                viewModel.HasExistingPropertyToSell = unitPurchaser.Financials.HasExistingPropertyToSell;
                viewModel.ExistingPropertyValue = unitPurchaser.Financials.ExistingPropertyValue;
                viewModel.ExistingMortgageBalance = unitPurchaser.Financials.ExistingMortgageBalance;
                viewModel.IsPropertyListed = unitPurchaser.Financials.IsPropertyListed;
                viewModel.ExpectedSaleDate = unitPurchaser.Financials.ExpectedSaleDate;
            }

            return View(viewModel);
        }

        // POST: /Purchaser/SubmitFinancials
        [HttpPost]
        [Authorize(Roles = "Purchaser")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitFinancials(SubmitFinancialsViewModel model)
        {
            var userId = _userManager.GetUserId(User);
            
            var unitPurchaser = await _context.UnitPurchasers
                .Include(up => up.Unit)
                .Include(up => up.Financials)
                .FirstOrDefaultAsync(up => up.UnitId == model.UnitId && up.PurchaserId == userId);

            if (unitPurchaser == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Create or update financials
            if (unitPurchaser.Financials == null)
            {
                unitPurchaser.Financials = new PurchaserFinancials
                {
                    UnitPurchaserId = unitPurchaser.Id,
                    CreatedAt = DateTime.UtcNow
                };
                _context.PurchaserFinancials.Add(unitPurchaser.Financials);
            }

            unitPurchaser.Financials.AdditionalCashAvailable = model.AdditionalCashAvailable;
            unitPurchaser.Financials.AdditionalCashAvailable = model.AdditionalCashAvailable ?? 0;
            unitPurchaser.Financials.RRSPAvailable = model.RRSPAvailable ?? 0;
            unitPurchaser.Financials.GiftFromFamily = model.GiftFromFamily ?? 0;
            unitPurchaser.Financials.ProceedsFromSale = model.ProceedsFromSale ?? 0;
            unitPurchaser.Financials.OtherFundsAmount = model.OtherFundsAmount ?? 0; unitPurchaser.Financials.OtherFundsDescription = model.OtherFundsDescription;
            unitPurchaser.Financials.HasExistingPropertyToSell = model.HasExistingPropertyToSell;
            unitPurchaser.Financials.ExistingPropertyValue = model.ExistingPropertyValue;
            unitPurchaser.Financials.ExistingMortgageBalance = model.ExistingMortgageBalance;
            unitPurchaser.Financials.IsPropertyListed = model.IsPropertyListed;
            unitPurchaser.Financials.ExpectedSaleDate = model.ExpectedSaleDate;
            unitPurchaser.Financials.UpdatedAt = DateTime.UtcNow;

            // Calculate total available funds
            unitPurchaser.Financials.TotalFundsAvailable =
                (model.AdditionalCashAvailable ?? 0) +
                (model.RRSPAvailable ?? 0) +
                (model.GiftFromFamily ?? 0) +
                (model.ProceedsFromSale ?? 0) +
                (model.OtherFundsAmount ?? 0);

            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "PurchaserFinancials",
                EntityId = model.UnitId,
                Action = "Submit",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = "Purchaser",
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { unitPurchaser.Financials.TotalFundsAvailable }),
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            // Recalculate shortfall
            try
            {
                await _shortfallService.AnalyzeShortfallAsync(model.UnitId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recalculating shortfall for unit {UnitId}", model.UnitId);
            }

            _logger.LogInformation("Purchaser {UserId} submitted financials for unit {UnitId}", userId, model.UnitId);

            TempData["Success"] = "Financial information saved successfully!";
            return RedirectToAction(nameof(Dashboard));
        }

        // GET: /Purchaser/ViewSOA/5 (UnitId)
        [Authorize(Roles = "Purchaser")]
        public async Task<IActionResult> ViewSOA(int id)
        {
            var userId = _userManager.GetUserId(User);
            
            var unitPurchaser = await _context.UnitPurchasers
                .Include(up => up.Unit)
                    .ThenInclude(u => u.Project)
                .Include(up => up.Unit)
                    .ThenInclude(u => u.SOA)
                .Include(up => up.Unit)
                    .ThenInclude(u => u.Deposits)
                .FirstOrDefaultAsync(up => up.UnitId == id && up.PurchaserId == userId);

            if (unitPurchaser == null)
            {
                return NotFound();
            }

            if (unitPurchaser.Unit.SOA == null)
            {
                TempData["Info"] = "Statement of Adjustments is not yet available. Please check back later.";
                return RedirectToAction(nameof(Dashboard));
            }

            return View(unitPurchaser);
        }
        // GET: /Purchaser/DownloadSOA/5
        public async Task<IActionResult> DownloadSOA(int id)
        {
            var userId = _userManager.GetUserId(User);

            // Get the unit purchaser relationship
            var unitPurchaser = await _context.UnitPurchasers
                .Include(up => up.Unit)
                    .ThenInclude(u => u.Project)
                .Include(up => up.Unit)
                    .ThenInclude(u => u.SOA)
                .Include(up => up.Unit)
                    .ThenInclude(u => u.Deposits)
                        .ThenInclude(d => d.InterestPeriods)
                .Include(up => up.Unit)
                    .ThenInclude(u => u.Purchasers)
                        .ThenInclude(p => p.Purchaser)
                .Include(up => up.Purchaser)
                .FirstOrDefaultAsync(up => up.UnitId == id && up.PurchaserId == userId);

            if (unitPurchaser == null)
                return Forbid();

            var unit = unitPurchaser.Unit;

            if (unit.SOA == null)
            {
                TempData["Error"] = "Statement of Adjustments is not yet available.";
                return RedirectToAction(nameof(ViewSOA), new { id });
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

            _logger.LogInformation("Purchaser {UserId} downloaded SOA for Unit {UnitId}", userId, id);

            return File(pdfBytes, "application/pdf", fileName);
        }

        // ============================================
        // DOCUMENT MANAGEMENT ACTIONS
        // ============================================

        /// <summary>
        /// Display the Manage Documents page for a unit
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ManageDocuments(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Verify purchaser has access to this unit
            var unitPurchaser = await _context.UnitPurchasers
                .Include(up => up.Unit)
                    .ThenInclude(u => u.Project)
                .Include(up => up.Unit)
                    .ThenInclude(u => u.Documents)
                .FirstOrDefaultAsync(up => up.UnitId == id && up.PurchaserId == userId);

            if (unitPurchaser == null)
            {
                return NotFound("Unit not found or access denied.");
            }

            var unit = unitPurchaser.Unit;

            // Define required document types for purchasers
            var requiredDocTypes = new List<(DocumentType type, string name, string desc, bool required)>
    {
        (DocumentType.MortgageApproval, "Mortgage Approval Letter", "Your mortgage approval or pre-approval letter from your lender", true),
        (DocumentType.IdentificationFront, "Government ID (Front)", "Front of your driver's license, passport, or other government-issued ID", true),
        (DocumentType.IdentificationBack, "Government ID (Back)", "Back of your government-issued ID (if applicable)", false),
        (DocumentType.BankStatement, "Bank Statement", "Recent bank statement showing available funds", true),
        (DocumentType.EmploymentLetter, "Employment Letter", "Letter confirming your employment and income", false),
        (DocumentType.NOA, "Notice of Assessment (NOA)", "Most recent CRA Notice of Assessment", false),
        (DocumentType.Other, "Other Supporting Documents", "Any other documents relevant to your purchase", false)
    };

            // Get uploaded documents by this purchaser
            var uploadedDocs = unit.Documents
                .Where(d => d.UploadedById == userId && d.Source == DocumentSource.Purchaser)
                .Select(d => new DocumentViewModel
                {
                    Id = d.Id,
                    FileName = d.FileName,
                    DocumentType = d.DocumentType,
                    DocumentTypeName = GetDocumentTypeName(d.DocumentType),
                    Description = d.Description,
                    FileSize = d.FileSize,
                    UploadedAt = d.UploadedAt,
                    CanDelete = true
                })
                .ToList();

            var viewModel = new ManageDocumentsViewModel
            {
                UnitId = unit.Id,
                UnitNumber = unit.UnitNumber,
                ProjectName = unit.Project.Name,
                UploadedDocuments = uploadedDocs,
                TotalUploaded = uploadedDocs.Count,
                TotalRequired = 3, // Mortgage, ID, Bank Statement
                RequiredDocuments = requiredDocTypes.Select(r => new RequiredDocumentInfo
                {
                    DocumentType = r.type,
                    Name = r.name,
                    Description = r.desc,
                    IsRequired = r.required,
                    IsUploaded = uploadedDocs.Any(d => d.DocumentType == r.type),
                    UploadedDocument = uploadedDocs.FirstOrDefault(d => d.DocumentType == r.type)
                }).ToList()
            };

            return View(viewModel);
        }

        /// <summary>
        /// Upload a document (AJAX)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadDocument([FromForm] UploadDocumentViewModel model)
        {
            var response = new DocumentUploadResponse();

            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Verify access
                var unitPurchaser = await _context.UnitPurchasers
                    .Include(up => up.Unit)
                    .FirstOrDefaultAsync(up => up.UnitId == model.UnitId && up.PurchaserId == userId);

                if (unitPurchaser == null)
                {
                    response.Message = "Access denied.";
                    return Json(response);
                }

                // Validate file
                if (model.File == null || model.File.Length == 0)
                {
                    response.Message = "Please select a file to upload.";
                    return Json(response);
                }

                // Validate file size (max 10MB)
                const long maxFileSize = 10 * 1024 * 1024;
                if (model.File.Length > maxFileSize)
                {
                    response.Message = "File size cannot exceed 10MB.";
                    return Json(response);
                }

                // Validate file type
                var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
                var extension = Path.GetExtension(model.File.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    response.Message = "Invalid file type. Allowed: PDF, JPG, PNG, DOC, DOCX";
                    return Json(response);
                }

                // Create uploads directory if it doesn't exist
                var uploadsPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "documents", model.UnitId.ToString());
                Directory.CreateDirectory(uploadsPath);

                // Generate unique filename
                var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsPath, uniqueFileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.File.CopyToAsync(stream);
                }

                // Create document record
                var document = new Document
                {
                    UnitId = model.UnitId,
                    FileName = model.File.FileName,
                    FilePath = $"/uploads/documents/{model.UnitId}/{uniqueFileName}",
                    ContentType = model.File.ContentType,
                    FileSize = model.File.Length,
                    DocumentType = model.DocumentType,
                    Source = DocumentSource.Purchaser,
                    Description = model.Description,
                    UploadedById = userId,
                    UploadedAt = DateTime.UtcNow
                };

                _context.Documents.Add(document);
                await _context.SaveChangesAsync();

                _context.AuditLogs.Add(new AuditLog
                {
                    EntityType = "Document",
                    EntityId = document.Id,
                    Action = "Upload",
                    UserId = userId,
                    UserName = User.Identity?.Name,
                    UserRole = "Purchaser",
                    NewValues = System.Text.Json.JsonSerializer.Serialize(new { document.FileName, document.DocumentType, document.FileSize }),
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                var uploader = await _userManager.GetUserAsync(User);
                var uploadedBy = $"{uploader?.FirstName} {uploader?.LastName}".Trim();

                await _notificationService.NotifyDocumentUploadedAsync(
                    unitId: model.UnitId,
                    documentName: document.FileName,
                    uploadedBy: uploadedBy
                );


                // Log the upload
                _logger.LogInformation("Document uploaded: {FileName} (Type: {Type}) for Unit {UnitId} by User {UserId}",
                    document.FileName, document.DocumentType, model.UnitId, userId);

                response.Success = true;
                response.Message = "Document uploaded successfully!";
                response.Document = new DocumentViewModel
                {
                    Id = document.Id,
                    FileName = document.FileName,
                    DocumentType = document.DocumentType,
                    DocumentTypeName = GetDocumentTypeName(document.DocumentType),
                    Description = document.Description,
                    FileSize = document.FileSize,
                    UploadedAt = document.UploadedAt,
                    CanDelete = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document");
                response.Message = "An error occurred while uploading the document.";
            }

            return Json(response);
        }

        /// <summary>
        /// Delete a document (AJAX)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDocument(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == id && d.UploadedById == userId);

            if (document == null)
            {
                return Json(new { success = false, message = "Document not found or access denied." });
            }

            try
            {
                // Delete physical file
                var fullPath = Path.Combine(_webHostEnvironment.WebRootPath, document.FilePath.TrimStart('/'));
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }

                // Delete database record
                _context.Documents.Remove(document);
                await _context.SaveChangesAsync();

                // Log deletion
                // Log deletion
                _logger.LogInformation("Document deleted: {FileName} (ID: {Id}) by User {UserId}",
                    document.FileName, id, userId);

                return Json(new { success = true, message = "Document deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document {DocumentId}", id);
                return Json(new { success = false, message = "An error occurred while deleting the document." });
            }
        }

        /// <summary>
        /// Download/View a document
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DownloadDocument(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Purchaser can only download their own documents
            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == id && d.UploadedById == userId);

            if (document == null)
            {
                return NotFound("Document not found or access denied.");
            }

            var fullPath = Path.Combine(_webHostEnvironment.WebRootPath, document.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound("File not found on server.");
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            return File(fileBytes, document.ContentType, document.FileName);
        }

        /// <summary>
        /// Helper method to get friendly document type name
        /// </summary>
        private static string GetDocumentTypeName(DocumentType type)
        {
            return type switch
            {
                DocumentType.AgreementOfPurchaseSale => "Agreement of Purchase & Sale",
                DocumentType.Amendment => "Amendment",
                DocumentType.DepositReceipt => "Deposit Receipt",
                DocumentType.MortgageApproval => "Mortgage Approval",
                DocumentType.Appraisal => "Appraisal",
                DocumentType.IdentificationFront => "ID (Front)",
                DocumentType.IdentificationBack => "ID (Back)",
                DocumentType.BankStatement => "Bank Statement",
                DocumentType.EmploymentLetter => "Employment Letter",
                DocumentType.NOA => "Notice of Assessment",
                DocumentType.SOA => "Statement of Adjustments",
                DocumentType.Other => "Other",
                _ => type.ToString()
            };
        }

        // GET: /Purchaser/AuditTrail
        [Authorize(Roles = "Purchaser")]
        public async Task<IActionResult> AuditTrail()
        {
            var userId = _userManager.GetUserId(User);
            var logs = await _context.AuditLogs
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
            return View(logs);
        }

        // GET: /Purchaser/SubmitExtensionRequest/5 (UnitId)
        [Authorize(Roles = "Purchaser")]
        public async Task<IActionResult> SubmitExtensionRequest(int id)
        {
            var userId = _userManager.GetUserId(User);

            var unitPurchaser = await _context.UnitPurchasers
                .Include(up => up.Unit)
                    .ThenInclude(u => u.Project)
                .Include(up => up.Unit)
                    .ThenInclude(u => u.ExtensionRequests)
                .FirstOrDefaultAsync(up => up.UnitId == id && up.PurchaserId == userId);

            if (unitPurchaser == null)
                return NotFound();

            var pendingRequest = unitPurchaser.Unit.ExtensionRequests
                .FirstOrDefault(r => r.Status == ClosingExtensionStatus.Pending);

            var viewModel = new SubmitExtensionRequestViewModel
            {
                UnitId = id,
                UnitNumber = unitPurchaser.Unit.UnitNumber,
                ProjectName = unitPurchaser.Unit.Project.Name,
                CurrentClosingDate = unitPurchaser.Unit.ClosingDate,
                ExistingRequestId = pendingRequest?.Id,
                RequestedNewClosingDate = pendingRequest?.RequestedNewClosingDate
                    ?? unitPurchaser.Unit.ClosingDate?.AddMonths(1) ?? DateTime.Today.AddMonths(1),
                Reason = pendingRequest?.Reason ?? ""
            };

            return View(viewModel);
        }

        // POST: /Purchaser/SubmitExtensionRequest
        [HttpPost]
        [Authorize(Roles = "Purchaser")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitExtensionRequest(SubmitExtensionRequestViewModel model)
        {
            var userId = _userManager.GetUserId(User);

            var unitPurchaser = await _context.UnitPurchasers
                .Include(up => up.Unit)
                    .ThenInclude(u => u.Project)
                .FirstOrDefaultAsync(up => up.UnitId == model.UnitId && up.PurchaserId == userId);

            if (unitPurchaser == null)
                return NotFound();

            if (!ModelState.IsValid)
                return View(model);

            // Cancel any existing pending request before creating a new one
            var existingPending = await _context.ClosingExtensionRequests
                .Where(r => r.UnitId == model.UnitId && r.Status == ClosingExtensionStatus.Pending)
                .ToListAsync();

            foreach (var existing in existingPending)
                existing.Status = ClosingExtensionStatus.Rejected;

            _context.ClosingExtensionRequests.Add(new ClosingExtensionRequest
            {
                UnitId = model.UnitId,
                RequestedByPurchaserId = userId!,
                RequestedDate = DateTime.UtcNow,
                OriginalClosingDate = unitPurchaser.Unit.ClosingDate,
                RequestedNewClosingDate = model.RequestedNewClosingDate,
                Reason = model.Reason,
                Status = ClosingExtensionStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "ClosingExtensionRequest",
                EntityId = model.UnitId,
                Action = "Submit",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = "Purchaser",
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { model.RequestedNewClosingDate, model.Reason }),
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            _logger.LogInformation("Purchaser {UserId} submitted extension request for unit {UnitId}", userId, model.UnitId);

            TempData["Success"] = "Your closing extension request has been submitted to the builder for review.";
            return RedirectToAction(nameof(Dashboard));
        }


    }
}
