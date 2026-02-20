using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PreConHub.Data;
using PreConHub.Models.Entities;
using PreConHub.Models.ViewModels;
using PreConHub.Services;

namespace PreConHub.Controllers
{
    [Authorize(Roles = "MarketingAgency")]
    public class MarketingAgencyController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<MarketingAgencyController> _logger;
        private readonly INotificationService _notificationService;

        public MarketingAgencyController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            INotificationService notificationService,
            ILogger<MarketingAgencyController> logger)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
            _logger = logger;
        }

        // GET: /MarketingAgency/Dashboard
        // Lists projects assigned to this MA user via Project.MarketingAgencyUserId (spec Section H)
        public async Task<IActionResult> Dashboard()
        {
            var currentUserId = _userManager.GetUserId(User);
            var projects = await _context.Projects
                .Include(p => p.Units)
                .Where(p => p.AllowMarketingAccess && p.MarketingAgencyUserId == currentUserId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var vm = new MarketingAgencyDashboardViewModel
            {
                Projects = projects.Select(p => new MarketingAgencyProjectItemViewModel
                {
                    ProjectId = p.Id,
                    ProjectName = p.Name,
                    Address = p.Address,
                    City = p.City,
                    Status = p.Status,
                    TotalUnits = p.Units.Count,
                    UnitsNeedingDiscount = p.Units.Count(u =>
                        u.Recommendation == ClosingRecommendation.CloseWithDiscount ||
                        u.Recommendation == ClosingRecommendation.CombinationSuggestion),
                    ClosingDate = p.ClosingDate
                }).ToList()
            };

            return View(vm);
        }

        // GET: /MarketingAgency/ProjectUnits/5
        // Design/pricing view only — no SOA, mortgage, or financial data (spec Section H)
        public async Task<IActionResult> ProjectUnits(int id)
        {
            var project = await _context.Projects
                .Include(p => p.Units)
                    .ThenInclude(u => u.ShortfallAnalysis)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            if (!project.AllowMarketingAccess)
                return Forbid();

            // Load any discount suggestions this MA user has previously submitted
            var maUserId = _userManager.GetUserId(User);
            var maSuggestions = await _context.AuditLogs
                .Where(a => a.EntityType == "Unit"
                         && a.Action == "SuggestDiscount"
                         && a.UserId == maUserId)
                .ToListAsync();

            var vm = new MarketingAgencyProjectUnitsViewModel
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                Address = $"{project.Address}, {project.City}",
                Units = project.Units
                    .OrderBy(u => u.UnitNumber)
                    .Select(u =>
                    {
                        var suggestion = maSuggestions.FirstOrDefault(s => s.EntityId == u.Id);
                        return new MarketingAgencyUnitItemViewModel
                        {
                            UnitId = u.Id,
                            UnitNumber = u.UnitNumber,
                            UnitType = u.UnitType,
                            Bedrooms = u.Bedrooms,
                            Bathrooms = u.Bathrooms,
                            SquareFootage = u.SquareFootage,
                            PurchasePrice = u.PurchasePrice,
                            ClosingDate = u.ClosingDate,
                            Recommendation = u.Recommendation,
                            AISuggestedDiscount = u.ShortfallAnalysis?.SuggestedDiscount,
                            HasMASuggestion = suggestion != null,
                            MASuggestionJson = suggestion?.NewValues
                        };
                    })
                    .ToList()
            };

            return View(vm);
        }

        // POST: /MarketingAgency/SuggestDiscount
        // Logs MA discount suggestion in AuditLog (spec Section A.3, I step 4)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuggestDiscount(SuggestDiscountViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please correct the form errors and try again.";
                return RedirectToAction(nameof(ProjectUnits), new { id = model.ProjectId });
            }

            var unit = await _context.Units
                .Include(u => u.Project)
                .FirstOrDefaultAsync(u => u.Id == model.UnitId);

            if (unit == null)
                return NotFound();

            if (!unit.Project.AllowMarketingAccess)
                return Forbid();

            var userId = _userManager.GetUserId(User);

            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "Unit",
                EntityId = unit.Id,
                Action = "SuggestDiscount",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = "MarketingAgency",
                NewValues = System.Text.Json.JsonSerializer.Serialize(new
                {
                    suggestedAmount = model.SuggestedAmount,
                    notes = model.Notes,
                    unitNumber = unit.UnitNumber,
                    projectName = unit.Project.Name
                }),
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            _logger.LogInformation("Marketing Agency {UserId} suggested discount of {Amount} for unit {UnitId}",
                userId, model.SuggestedAmount, unit.Id);

            // Notify builder
            var maUser = await _userManager.GetUserAsync(User);
            var maName = $"{maUser?.FirstName} {maUser?.LastName}".Trim();
            await _notificationService.NotifyMarketingAgencySuggestionAsync(unit.ProjectId, maName, unit.Project.BuilderId);

            TempData["Success"] = $"Discount suggestion of {model.SuggestedAmount:C0} submitted for Unit {model.UnitNumber}.";
            return RedirectToAction(nameof(ProjectUnits), new { id = model.ProjectId });
        }

        // POST: /MarketingAgency/SuggestCreditAdjustment
        // Logs MA credit adjustment suggestion in AuditLog (spec Section A.3)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuggestCreditAdjustment(SuggestDiscountViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please correct the form errors and try again.";
                return RedirectToAction(nameof(ProjectUnits), new { id = model.ProjectId });
            }

            var unit = await _context.Units
                .Include(u => u.Project)
                .FirstOrDefaultAsync(u => u.Id == model.UnitId);

            if (unit == null)
                return NotFound();

            if (!unit.Project.AllowMarketingAccess)
                return Forbid();

            var userId = _userManager.GetUserId(User);

            _context.AuditLogs.Add(new AuditLog
            {
                EntityType = "Unit",
                EntityId = unit.Id,
                Action = "SuggestCreditAdjustment",
                UserId = userId,
                UserName = User.Identity?.Name,
                UserRole = "MarketingAgency",
                NewValues = System.Text.Json.JsonSerializer.Serialize(new
                {
                    suggestedAmount = model.SuggestedAmount,
                    notes = model.Notes,
                    unitNumber = unit.UnitNumber,
                    projectName = unit.Project.Name
                }),
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            // Notify builder
            var maUser2 = await _userManager.GetUserAsync(User);
            var maName2 = $"{maUser2?.FirstName} {maUser2?.LastName}".Trim();
            await _notificationService.NotifyMarketingAgencySuggestionAsync(unit.ProjectId, maName2, unit.Project.BuilderId);

            TempData["Success"] = $"Credit adjustment suggestion of {model.SuggestedAmount:C0} submitted for Unit {model.UnitNumber}.";
            return RedirectToAction(nameof(ProjectUnits), new { id = model.ProjectId });
        }

        // GET: /MarketingAgency/SuggestionHistory/5
        // Shows MA suggestion history for a project (spec Section H)
        public async Task<IActionResult> SuggestionHistory(int id)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            if (!project.AllowMarketingAccess || project.MarketingAgencyUserId != currentUserId)
                return Forbid();

            var unitIds = await _context.Units
                .Where(u => u.ProjectId == id)
                .Select(u => u.Id)
                .ToListAsync();

            var suggestions = await _context.AuditLogs
                .Where(a => unitIds.Contains(a.EntityId)
                         && a.UserId == currentUserId
                         && (a.Action == "SuggestDiscount" || a.Action == "SuggestCreditAdjustment"))
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();

            ViewBag.ProjectId = id;
            ViewBag.ProjectName = project.Name;
            return View(suggestions);
        }

        // GET: /MarketingAgency/AuditTrail
        // MA's own action log (spec Section H — audit trail visible to own actions only)
        public async Task<IActionResult> AuditTrail()
        {
            var userId = _userManager.GetUserId(User);

            var logs = await _context.AuditLogs
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();

            return View(logs);
        }
    }
}
