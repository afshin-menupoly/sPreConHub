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
        public async Task<IActionResult> History()
        {
            var userId = _userManager.GetUserId(User);

            var query = _context.ClosingExtensionRequests
                .Include(r => r.Unit).ThenInclude(u => u.Project)
                .Include(r => r.RequestedByPurchaser)
                .Include(r => r.ReviewedByBuilder)
                .Where(r => r.Status != ClosingExtensionStatus.Pending);

            if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
                query = query.Where(r => r.Unit.Project.BuilderId == userId);

            var requests = await query
                .OrderByDescending(r => r.ReviewedAt ?? r.RequestedDate)
                .ToListAsync();

            var vm = new ExtensionRequestListViewModel
            {
                PageTitle = "Extension Request History",
                ShowingHistory = true,
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
