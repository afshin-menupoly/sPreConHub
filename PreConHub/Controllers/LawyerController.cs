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
    public class LawyerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<LawyerController> _logger;
        private readonly IEmailService _emailService;
        private readonly IPdfService _pdfService;
        private readonly INotificationService _notificationService;
        private readonly IWebHostEnvironment _environment;


        public LawyerController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailService emailService,
            IPdfService pdfService,
            INotificationService notificationService,
            IWebHostEnvironment environment,
            ILogger<LawyerController> logger)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _pdfService = pdfService;
            _notificationService = notificationService;
            _environment = environment;
            _logger = logger;
        }

        // GET: /Lawyer/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.GetUserAsync(User);

            // Get all units assigned to this lawyer
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
                    .ThenInclude(u => u.SOA)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.ShortfallAnalysis)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Deposits)
                .Where(la => la.LawyerId == userId && la.IsActive)
                .ToListAsync();

            var viewModel = new LawyerDashboardViewModel
            {
                LawyerName = $"{user?.FirstName} {user?.LastName}",
                LawyerFirm = user?.CompanyName,
                Units = assignments.Select(la => new LawyerUnitViewModel
                {
                    AssignmentId = la.Id,
                    UnitId = la.Unit.Id,
                    UnitNumber = la.Unit.UnitNumber,
                    ProjectName = la.Unit.Project.Name,
                    ProjectAddress = $"{la.Unit.Project.Address}, {la.Unit.Project.City}",
                    PurchasePrice = la.Unit.PurchasePrice,
                    ClosingDate = la.Unit.ClosingDate ?? la.Unit.Project.ClosingDate,
                    
                    // Purchaser Info
                    PurchaserName = la.Unit.Purchasers
                        .Where(p => p.IsPrimaryPurchaser)
                        .Select(p => $"{p.Purchaser.FirstName} {p.Purchaser.LastName}")
                        .FirstOrDefault() ?? "Not Assigned",
                    PurchaserEmail = la.Unit.Purchasers
                        .Where(p => p.IsPrimaryPurchaser)
                        .Select(p => p.Purchaser.Email)
                        .FirstOrDefault(),
                    
                    // Status
                    UnitStatus = la.Unit.Status,
                    ReviewStatus = la.ReviewStatus,
                    AssignedAt = la.AssignedAt,
                    ReviewedAt = la.ReviewedAt,
                    
                    // SOA Info
                    HasSOA = la.Unit.SOA != null,
                    BalanceDueOnClosing = la.Unit.SOA?.BalanceDueOnClosing ?? 0,
                    CashRequiredToClose = la.Unit.SOA?.CashRequiredToClose ?? 0,
                    LawyerUploadedBalanceDue = la.Unit.SOA?.LawyerUploadedBalanceDue,
                    
                    // Shortfall
                    ShortfallAmount = la.Unit.ShortfallAnalysis?.ShortfallAmount ?? 0,
                    Recommendation = la.Unit.Recommendation,
                    
                    // Mortgage
                    HasMortgageApproval = la.Unit.Purchasers
                        .Any(p => p.MortgageInfo != null && p.MortgageInfo.HasMortgageApproval),
                    MortgageAmount = la.Unit.Purchasers
                        .Where(p => p.MortgageInfo != null)
                        .Sum(p => p.MortgageInfo!.ApprovedAmount ?? 0m),
                    // Deposits
                    TotalDeposits = la.Unit.Deposits.Sum(d => d.Amount),
                    DepositsPaid = la.Unit.Deposits.Where(d => d.IsPaid).Sum(d => d.Amount),
                    
                    // Days until closing
                    DaysUntilClosing = la.Unit.ClosingDate.HasValue 
                        ? (int)(la.Unit.ClosingDate.Value - DateTime.Now).TotalDays 
                        : 0
                }).ToList()
            };

            // Calculate summary stats
            viewModel.TotalAssigned = viewModel.Units.Count;
            viewModel.PendingReview = viewModel.Units.Count(u => u.ReviewStatus == LawyerReviewStatus.Pending);
            viewModel.UnderReview = viewModel.Units.Count(u => u.ReviewStatus == LawyerReviewStatus.UnderReview);
            viewModel.Approved = viewModel.Units.Count(u => u.ReviewStatus == LawyerReviewStatus.Approved);
            viewModel.NeedsAttention = viewModel.Units.Count(u => u.ReviewStatus == LawyerReviewStatus.NeedsRevision);

            return View(viewModel);
        }

        // GET: /Lawyer/ReviewUnit/5 (AssignmentId)
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
                .Include(la => la.LawyerNotes)
                .FirstOrDefaultAsync(la => la.Id == id && la.LawyerId == userId);

            if (assignment == null)
                return NotFound();

            var unit = assignment.Unit;
            var primaryPurchaser = unit.Purchasers.FirstOrDefault(p => p.IsPrimaryPurchaser);

            var viewModel = new LawyerReviewViewModel
            {
                AssignmentId = assignment.Id,
                UnitId = unit.Id,
                
                // Project Info
                ProjectName = unit.Project.Name,
                ProjectAddress = $"{unit.Project.Address}, {unit.Project.City}, ON {unit.Project.PostalCode}",
                BuilderName = unit.Project.BuilderCompanyName,

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
                
                // Mortgage Info
                HasMortgageApproval = primaryPurchaser?.MortgageInfo?.HasMortgageApproval ?? false,
                MortgageProvider = primaryPurchaser?.MortgageInfo?.MortgageProvider,
                MortgageAmount = primaryPurchaser?.MortgageInfo?.ApprovedAmount ?? 0,
                MortgageApprovalType = primaryPurchaser?.MortgageInfo?.ApprovalType ?? MortgageApprovalType.None,
                MortgageExpiryDate = primaryPurchaser?.MortgageInfo?.ApprovalExpiryDate,
                MortgageConditions = primaryPurchaser?.MortgageInfo?.Conditions,
                
                // Financial Info
                AdditionalCashAvailable = primaryPurchaser?.Financials?.AdditionalCashAvailable ?? 0,
                TotalFundsAvailable = primaryPurchaser?.Financials?.TotalFundsAvailable ?? 0,
                
                // Deposits
                Deposits = unit.Deposits.Select(d => new DepositViewModel
                {
                    Id = d.Id,
                    DepositName = d.DepositName,
                    Amount = d.Amount,
                    DueDate = d.DueDate,
                    PaidDate = d.PaidDate,
                    IsPaid = d.IsPaid,
                    Status = d.Status
                }).OrderBy(d => d.DueDate).ToList(),
                TotalDeposits = unit.Deposits.Sum(d => d.Amount),
                DepositsPaid = unit.Deposits.Where(d => d.IsPaid).Sum(d => d.Amount),
                
                // SOA
                HasSOA = unit.SOA != null,
                SOA = unit.SOA,
                
                // Shortfall
                ShortfallAmount = unit.ShortfallAnalysis?.ShortfallAmount ?? 0,
                ShortfallPercentage = unit.ShortfallAnalysis?.ShortfallPercentage ?? 0,
                Recommendation = unit.Recommendation,
                RecommendationReasoning = unit.ShortfallAnalysis?.RecommendationReasoning,
                
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
                        CreatedAt = n.CreatedAt
                    }).ToList() ?? new List<LawyerNoteViewModel>()
            };

            return View(viewModel);
        }

        // POST: /Lawyer/StartReview/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartReview(int id)
        {
            var userId = _userManager.GetUserId(User);
            var assignment = await _context.LawyerAssignments
                .FirstOrDefaultAsync(la => la.Id == id && la.LawyerId == userId);

            if (assignment == null)
                return NotFound();

            assignment.ReviewStatus = LawyerReviewStatus.UnderReview;
            assignment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Review started. You can now add notes and complete your review.";
            return RedirectToAction(nameof(ReviewUnit), new { id });
        }

        // POST: /Lawyer/ApproveUnit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveUnit(int id, string? approvalNotes)
        {
            var userId = _userManager.GetUserId(User);
            var assignment = await _context.LawyerAssignments
                .Include(la => la.Unit)
                .FirstOrDefaultAsync(la => la.Id == id && la.LawyerId == userId);

            if (assignment == null)
                return NotFound();

            assignment.ReviewStatus = LawyerReviewStatus.Approved;
            assignment.ReviewedAt = DateTime.UtcNow;
            assignment.UpdatedAt = DateTime.UtcNow;

            // Update unit status
            assignment.Unit.LawyerConfirmed = true;
            assignment.Unit.LawyerConfirmedAt = DateTime.UtcNow;
            assignment.Unit.UpdatedAt = DateTime.UtcNow;

            // Add approval note
            if (!string.IsNullOrWhiteSpace(approvalNotes))
            {
                var note = new LawyerNote
                {
                    LawyerAssignmentId = id,
                    Note = approvalNotes,
                    NoteType = LawyerNoteType.Approval,
                    CreatedAt = DateTime.UtcNow
                };
                _context.LawyerNotes.Add(note);
            }

            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "LawyerAssignment",
                EntityId = assignment.UnitId ?? 0,
                Action = "Approve",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = "Lawyer",
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var lawyer = await _userManager.GetUserAsync(User);
            var lawyerName = $"{lawyer?.FirstName} {lawyer?.LastName}".Trim();

            await _notificationService.NotifyLawyerApprovedAsync(
                unitId: assignment.Id,
                lawyerName: lawyerName,
                builderId: assignment.Unit.Project.BuilderId
            );

            var builderUser = await _context.Users.FindAsync(assignment.Unit.Project.BuilderId);
            if (builderUser != null && !string.IsNullOrEmpty(builderUser.Email))
            {
                var user = await _userManager.GetUserAsync(User);
                await _emailService.SendLawyerApprovalNotificationAsync(
                    builderUser.Email,
                    $"{builderUser.FirstName} {builderUser.LastName}",
                    $"{user?.FirstName} {user?.LastName}",
                    assignment.Unit.UnitNumber,
                    assignment.Unit.Project.Name
                );
            }

            _logger.LogInformation("Lawyer {UserId} approved unit {UnitId}", userId, assignment.UnitId);

            TempData["Success"] = $"Unit {assignment.Unit.UnitNumber} has been approved for closing.";
            return RedirectToAction(nameof(Dashboard));
        }

        // POST: /Lawyer/RequestRevision/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestRevision(int id, string revisionNotes)
        {
            var userId = _userManager.GetUserId(User);
            var assignment = await _context.LawyerAssignments
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Project)
                .FirstOrDefaultAsync(la => la.Id == id && la.LawyerId == userId);

            if (assignment == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(revisionNotes))
            {
                TempData["Error"] = "Please provide details about what needs to be revised.";
                return RedirectToAction(nameof(ReviewUnit), new { id });
            }

            assignment.ReviewStatus = LawyerReviewStatus.NeedsRevision;
            assignment.UpdatedAt = DateTime.UtcNow;

            // Add revision note
            var note = new LawyerNote
            {
                LawyerAssignmentId = id,
                Note = revisionNotes,
                NoteType = LawyerNoteType.RevisionRequest,
                CreatedAt = DateTime.UtcNow
            };
            _context.LawyerNotes.Add(note);

            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "LawyerAssignment",
                EntityId = assignment.UnitId ?? 0,
                Action = "RequestRevision",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = "Lawyer",
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { revisionNotes }),
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var builderUser = await _context.Users.FindAsync(assignment.Unit.Project.BuilderId);
            if (builderUser != null && !string.IsNullOrEmpty(builderUser.Email))
            {
                var user = await _userManager.GetUserAsync(User);
                await _emailService.SendLawyerRevisionRequestAsync(
                    builderUser.Email,
                    $"{builderUser.FirstName} {builderUser.LastName}",
                    $"{user?.FirstName} {user?.LastName}",
                    assignment.Unit.UnitNumber,
                    assignment.Unit.Project.Name,
                    revisionNotes
                );
            }

            var lawyer = await _userManager.GetUserAsync(User);
            var lawyerName = $"{lawyer?.FirstName} {lawyer?.LastName}".Trim();

            await _notificationService.NotifyLawyerRequestedRevisionAsync(
                unitId: assignment.Id,
                lawyerName: lawyerName,
                builderId: assignment.Unit.Project.BuilderId,
                notes: revisionNotes
            );


            _logger.LogInformation("Lawyer {UserId} requested revision for unit {UnitId}", userId, assignment.UnitId);

            TempData["Success"] = $"Revision requested for Unit {assignment.Unit.UnitNumber}. The builder will be notified.";
            return RedirectToAction(nameof(Dashboard));
        }

        // POST: /Lawyer/AddNote/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNote(int assignmentId, string note, LawyerNoteType noteType, NoteVisibility visibility = NoteVisibility.ForBuilder)
        {
            var assignment = await _context.LawyerAssignments
                .Include(la => la.Unit)
                .FirstOrDefaultAsync(la => la.Id == assignmentId);

            if (assignment == null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            if (assignment.LawyerId != userId)
                return Forbid();

            var lawyerNote = new LawyerNote
            {
                LawyerAssignmentId = assignmentId,
                Note = note,
                NoteType = noteType,
                Visibility = visibility,
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
                Action = "AddNote",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = "Lawyer",
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { noteType, visibility }),
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            _logger.LogInformation("Lawyer {LawyerId} added {NoteType} note to assignment {AssignmentId}",
                userId, noteType, assignmentId);

            TempData["Success"] = "Note added successfully.";
            return RedirectToAction(nameof(ReviewUnit), new { id = assignment.UnitId });
        }


        // GET: /Lawyer/AcceptInvitation (for lawyer invitation flow)
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

            // Get assignments to show on welcome page
            var assignments = await _context.LawyerAssignments
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Project)
                .Where(la => la.LawyerId == user.Id)
                .ToListAsync();

            var viewModel = new LawyerAcceptInvitationViewModel
            {
                Email = email,
                Code = code,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FirmName = user.CompanyName,
                AssignedUnitsCount = assignments.Count,
                ProjectNames = assignments.Select(a => a.Unit.Project.Name).Distinct().ToList()
            };

            return View(viewModel);
        }

        // POST: /Lawyer/AcceptInvitation
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

            //var decodedCode = System.Net.WebUtility.UrlDecode(model.Code);
            // Don't decode - the model binding already decoded it
            var result = await _userManager.ResetPasswordAsync(user, model.Code, model.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    if (error.Code == "InvalidToken")
                    {
                        ModelState.AddModelError("", "This invitation link has expired or is invalid. Please contact the builder for a new invitation.");
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

            _logger.LogInformation("Lawyer {Email} accepted invitation and activated account", model.Email);

            TempData["Success"] = "Welcome! Your account has been activated successfully.";
            return RedirectToAction(nameof(Dashboard));
        }

        // GET: /Lawyer/InvalidInvitation
        [AllowAnonymous]
        public IActionResult InvalidInvitation()
        {
            return View();
        }

        // POST: /Lawyer/UploadSOA
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadSOA(LawyerUploadSoaViewModel model)
        {
            var userId = _userManager.GetUserId(User);

            var assignment = await _context.LawyerAssignments
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Project)
                .Include(la => la.Unit)
                    .ThenInclude(u => u.SOA)
                .FirstOrDefaultAsync(la => la.Id == model.AssignmentId && la.LawyerId == userId);

            if (assignment == null)
                return NotFound();

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please correct the errors and try again.";
                return RedirectToAction(nameof(ReviewUnit), new { id = model.AssignmentId });
            }

            // Validate file extension
            var ext = Path.GetExtension(model.SoaFile.FileName).ToLowerInvariant();
            if (ext != ".pdf")
            {
                TempData["Error"] = "Only PDF files are accepted for SOA uploads.";
                return RedirectToAction(nameof(ReviewUnit), new { id = model.AssignmentId });
            }

            // Validate file size (max 10 MB)
            if (model.SoaFile.Length > 10 * 1024 * 1024)
            {
                TempData["Error"] = "File size cannot exceed 10 MB.";
                return RedirectToAction(nameof(ReviewUnit), new { id = model.AssignmentId });
            }

            var unit = assignment.Unit;

            // Save file to wwwroot/uploads/documents/{unitId}/{guid}.pdf
            var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads", "documents", unit.Id.ToString());
            Directory.CreateDirectory(uploadsDir);

            var savedFileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsDir, savedFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.SoaFile.CopyToAsync(stream);
            }

            // Create Document entity
            var document = new Document
            {
                UnitId = unit.Id,
                FileName = model.SoaFile.FileName,
                FilePath = $"/uploads/documents/{unit.Id}/{savedFileName}",
                FileSize = model.SoaFile.Length,
                ContentType = "application/pdf",
                DocumentType = DocumentType.SOA,
                Source = DocumentSource.Lawyer,
                Description = model.Description,
                UploadedAt = DateTime.UtcNow,
                UploadedById = userId!
            };
            _context.Documents.Add(document);

            // Update SOA with lawyer's balance due
            if (unit.SOA != null)
            {
                var oldBalance = unit.SOA.LawyerUploadedBalanceDue;
                unit.SOA.LawyerUploadedBalanceDue = model.LawyerBalanceDue;

                _context.AuditLogs.Add(new AuditLog
                {
                    EntityType = "StatementOfAdjustments",
                    EntityId = unit.Id,
                    Action = "LawyerUploadSOA",
                    UserId = userId,
                    UserName = User.Identity?.Name,
                    UserRole = "Lawyer",
                    OldValues = oldBalance.HasValue ? JsonSerializer.Serialize(new { LawyerUploadedBalanceDue = oldBalance }) : null,
                    NewValues = JsonSerializer.Serialize(new { LawyerUploadedBalanceDue = model.LawyerBalanceDue, DocumentId = document.Id }),
                    Timestamp = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            // Create SOAVersion record for lawyer upload
            var lastVersion = await _context.SOAVersions
                .Where(v => v.UnitId == unit.Id)
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => v.VersionNumber)
                .FirstOrDefaultAsync();

            _context.SOAVersions.Add(new SOAVersion
            {
                UnitId = unit.Id,
                VersionNumber = lastVersion + 1,
                Source = SOAVersionSource.LawyerUpload,
                BalanceDueOnClosing = model.LawyerBalanceDue,
                TotalVendorCredits = unit.SOA?.TotalVendorCredits ?? 0,
                TotalPurchaserCredits = unit.SOA?.TotalPurchaserCredits ?? 0,
                CashRequiredToClose = unit.SOA?.CashRequiredToClose ?? 0,
                UploadedFilePath = document.FilePath,
                CreatedByUserId = userId!,
                CreatedByRole = "Lawyer",
                CreatedAt = DateTime.UtcNow,
                Notes = $"Lawyer upload. Balance: {model.LawyerBalanceDue:C2}"
            });
            await _context.SaveChangesAsync();

            // Notify builder
            var lawyer = await _userManager.GetUserAsync(User);
            var lawyerName = $"{lawyer?.FirstName} {lawyer?.LastName}".Trim();

            await _notificationService.CreateAsync(
                userId: unit.Project.BuilderId,
                title: "Lawyer SOA Uploaded",
                message: $"{lawyerName} uploaded a final SOA for Unit {unit.UnitNumber} in {unit.Project.Name}. Balance due: {model.LawyerBalanceDue:C}",
                type: NotificationType.Info,
                actionUrl: $"/Units/Details/{unit.Id}",
                unitId: unit.Id
            );

            _logger.LogInformation("Lawyer {UserId} uploaded SOA for Unit {UnitId}, balance due: {Balance}",
                userId, unit.Id, model.LawyerBalanceDue);

            TempData["Success"] = "SOA document uploaded successfully.";
            return RedirectToAction(nameof(ReviewUnit), new { id = model.AssignmentId });
        }

        // GET: /Lawyer/DownloadSOA/5 (AssignmentId)
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
                .FirstOrDefaultAsync(la => la.Id == id && la.LawyerId == userId);

            if (assignment == null)
                return Forbid();

            var unit = assignment.Unit;

            if (unit.SOA == null)
            {
                TempData["Error"] = "Statement of Adjustments has not been generated.";
                return RedirectToAction(nameof(ReviewUnit), new { id });
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

            _logger.LogInformation("Lawyer {UserId} downloaded SOA for Unit {UnitId}", userId, unit.Id);

            return File(pdfBytes, "application/pdf", fileName);
        }

        // GET: /Lawyer/SOAVersionHistory/5 (UnitId)
        public async Task<IActionResult> SOAVersionHistory(int id)
        {
            var userId = _userManager.GetUserId(User);

            // Verify lawyer is assigned to this unit
            var assignment = await _context.LawyerAssignments
                .Include(la => la.Unit)
                    .ThenInclude(u => u.Project)
                .FirstOrDefaultAsync(la => la.UnitId == id && la.LawyerId == userId);

            if (assignment == null)
                return NotFound();

            var versions = await _context.SOAVersions
                .Where(v => v.UnitId == id)
                .Include(v => v.CreatedByUser)
                .OrderByDescending(v => v.VersionNumber)
                .ToListAsync();

            var vm = new SOAVersionHistoryViewModel
            {
                UnitId = id,
                UnitNumber = assignment.Unit.UnitNumber,
                ProjectName = assignment.Unit.Project.Name,
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

            return View("~/Views/Units/SOAVersionHistory.cshtml", vm);
        }

    }
}
