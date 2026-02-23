using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PreConHub.Data;
using PreConHub.Models.Entities;
using PreConHub.Models.ViewModels;

namespace PreConHub.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin,Builder")]
    public class ExtensionRequestController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ExtensionRequestController> _logger;

        public ExtensionRequestController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<ExtensionRequestController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: /ExtensionRequest — pending requests across builder's projects
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var query = _context.ClosingExtensionRequests
                .Include(r => r.Unit).ThenInclude(u => u.Project)
                .Include(r => r.RequestedByPurchaser)
                .Where(r => r.Status == ClosingExtensionStatus.Pending);

            // Non-admin sees only their projects
            if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
                query = query.Where(r => r.Unit.Project.BuilderId == userId);

            var requests = await query
                .OrderByDescending(r => r.RequestedDate)
                .ToListAsync();

            var vm = new ExtensionRequestListViewModel
            {
                PageTitle = "Pending Extension Requests",
                ShowingHistory = false,
                Requests = requests.Select(r => new ExtensionRequestItem
                {
                    RequestId = r.Id,
                    UnitId = r.UnitId,
                    UnitNumber = r.Unit.UnitNumber,
                    ProjectName = r.Unit.Project.Name,
                    PurchaserName = $"{r.RequestedByPurchaser.FirstName} {r.RequestedByPurchaser.LastName}".Trim(),
                    OriginalClosingDate = r.OriginalClosingDate,
                    RequestedNewClosingDate = r.RequestedNewClosingDate,
                    Reason = r.Reason,
                    RequestedDate = r.RequestedDate,
                    Status = r.Status
                }).ToList()
            };

            return View(vm);
        }

        // GET: /ExtensionRequest/History
        public async Task<IActionResult> History(string? projectFilter = null,
            string? search = null, string sortBy = "submitted", string sortDir = "desc")
        {
            var userId = _userManager.GetUserId(User);

            var query = _context.ClosingExtensionRequests
                .Include(r => r.Unit).ThenInclude(u => u.Project)
                .Include(r => r.RequestedByPurchaser)
                .Include(r => r.ReviewedByBuilder)
                .Where(r => r.Status != ClosingExtensionStatus.Pending);

            if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
                query = query.Where(r => r.Unit.Project.BuilderId == userId);

            var allRequests = await query.ToListAsync();

            // Build project list for dropdown (from all history, before filtering)
            var projects = allRequests
                .Select(r => r.Unit.Project.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            var totalRequests = allRequests.Count;

            // Project filter
            if (!string.IsNullOrWhiteSpace(projectFilter))
                allRequests = allRequests
                    .Where(r => r.Unit.Project.Name.Equals(projectFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            // Search by unit number (prefix) or purchaser name (partial)
            if (!string.IsNullOrWhiteSpace(search) && search.Trim().Length >= 1)
            {
                var term = search.Trim();
                allRequests = allRequests.Where(r =>
                    r.Unit.UnitNumber.StartsWith(term, StringComparison.OrdinalIgnoreCase) ||
                    $"{r.RequestedByPurchaser.FirstName} {r.RequestedByPurchaser.LastName}"
                        .Contains(term, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            // Validate sort params
            if (!new[] { "submitted", "unit", "status" }.Contains(sortBy)) sortBy = "submitted";
            if (sortDir != "asc") sortDir = "desc";

            // Sort
            var sorted = (sortBy, sortDir) switch
            {
                ("unit", "asc") => allRequests.OrderBy(r => r.Unit.UnitNumber).ToList(),
                ("unit", "desc") => allRequests.OrderByDescending(r => r.Unit.UnitNumber).ToList(),
                ("status", "asc") => allRequests.OrderBy(r => r.Status).ThenByDescending(r => r.RequestedDate).ToList(),
                ("status", "desc") => allRequests.OrderByDescending(r => r.Status).ThenByDescending(r => r.RequestedDate).ToList(),
                ("submitted", "asc") => allRequests.OrderBy(r => r.RequestedDate).ToList(),
                _ => allRequests.OrderByDescending(r => r.RequestedDate).ToList()
            };

            var vm = new ExtensionRequestListViewModel
            {
                PageTitle = "Extension Request History",
                ShowingHistory = true,
                ProjectFilter = projectFilter,
                SearchQuery = search,
                SortBy = sortBy,
                SortDir = sortDir,
                TotalRequests = totalRequests,
                Projects = projects,
                Requests = sorted.Select(r => new ExtensionRequestItem
                {
                    RequestId = r.Id,
                    UnitId = r.UnitId,
                    UnitNumber = r.Unit.UnitNumber,
                    ProjectName = r.Unit.Project.Name,
                    PurchaserName = $"{r.RequestedByPurchaser.FirstName} {r.RequestedByPurchaser.LastName}".Trim(),
                    OriginalClosingDate = r.OriginalClosingDate,
                    RequestedNewClosingDate = r.RequestedNewClosingDate,
                    Reason = r.Reason,
                    RequestedDate = r.RequestedDate,
                    Status = r.Status,
                    ReviewerNotes = r.ReviewerNotes,
                    ReviewedAt = r.ReviewedAt,
                    ReviewedByName = r.ReviewedByBuilder != null
                        ? $"{r.ReviewedByBuilder.FirstName} {r.ReviewedByBuilder.LastName}".Trim()
                        : null
                }).ToList()
            };

            return View("Index", vm);
        }

        // GET: /ExtensionRequest/PendingCount — returns JSON count for nav badge
        [HttpGet]
        public async Task<IActionResult> PendingCount()
        {
            var userId = _userManager.GetUserId(User);

            var query = _context.ClosingExtensionRequests
                .Where(r => r.Status == ClosingExtensionStatus.Pending);

            if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
                query = query.Where(r => r.Unit.Project.BuilderId == userId);

            var count = await query.CountAsync();
            return Json(new { count });
        }
    }
}
