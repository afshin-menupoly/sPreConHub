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
    [Authorize(Roles = "Admin,SuperAdmin,Builder")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IReportExportService _exportService;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IReportExportService exportService,
            ILogger<ReportsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _exportService = exportService;
            _logger = logger;
        }

        // ================================================================
        // GET: /Reports  (Hub / Landing Page)
        // ================================================================
        public async Task<IActionResult> Index()
        {
            var projects = await GetProjectsQuery().ToListAsync();

            var allUnits = projects.SelectMany(p => p.Units).ToList();

            var model = new ReportsHubViewModel
            {
                TotalProjects = projects.Count,
                TotalUnits = allUnits.Count,
                UnitsClosingClean = allUnits.Count(u =>
                    u.Recommendation == ClosingRecommendation.ProceedToClose),
                UnitsAtRisk = allUnits.Count(u =>
                    u.Recommendation == ClosingRecommendation.HighRiskDefault ||
                    u.Recommendation == ClosingRecommendation.PotentialDefault),
                TotalSalesValue = allUnits.Sum(u => u.PurchasePrice),
                TotalExposure = allUnits.Where(u => u.ShortfallAnalysis != null && u.ShortfallAnalysis.ShortfallAmount > 0)
                    .Sum(u => u.ShortfallAnalysis!.ShortfallAmount),
                Projects = projects.Select(p => new ProjectDropdownItem
                {
                    Id = p.Id,
                    Name = p.Name,
                    UnitCount = p.Units.Count,
                    Status = p.Status
                }).ToList()
            };

            return View(model);
        }

        // ================================================================
        // GET: /Reports/ProjectReport?projectId=5
        // ================================================================
        public async Task<IActionResult> ProjectReport(int? projectId)
        {
            var projects = await GetProjectsQuery().ToListAsync();
            if (!projects.Any())
                return RedirectToAction("Index");

            var project = projectId.HasValue
                ? projects.FirstOrDefault(p => p.Id == projectId.Value)
                : projects.First();

            if (project == null)
                return NotFound();

            var units = project.Units.ToList();
            var unitItems = units.Select(u => BuildReportUnitItem(u)).ToList();

            var model = new ProjectReportViewModel
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                ProjectAddress = project.Address,
                City = project.City,
                ProjectStatus = project.Status,
                ProjectType = project.ProjectType,
                ClosingDate = project.ClosingDate,
                OccupancyDate = project.OccupancyDate,
                BuilderCompanyName = project.BuilderCompanyName,

                AllProjects = projects.Select(p => new ProjectDropdownItem
                {
                    Id = p.Id, Name = p.Name, UnitCount = p.Units.Count, Status = p.Status
                }).ToList(),

                TotalUnits = units.Count,
                UnitsReadyToClose = units.Count(u => u.Recommendation == ClosingRecommendation.ProceedToClose),
                UnitsNeedingDiscount = units.Count(u => u.Recommendation == ClosingRecommendation.CloseWithDiscount),
                UnitsNeedingVTB = units.Count(u =>
                    u.Recommendation == ClosingRecommendation.VTBSecondMortgage ||
                    u.Recommendation == ClosingRecommendation.VTBFirstMortgage),
                UnitsAtRisk = units.Count(u =>
                    u.Recommendation == ClosingRecommendation.HighRiskDefault ||
                    u.Recommendation == ClosingRecommendation.PotentialDefault),
                UnitsPendingData = units.Count(u => u.Recommendation == null),
                UnitsMutualRelease = units.Count(u => u.Recommendation == ClosingRecommendation.MutualRelease),
                UnitsCombination = units.Count(u => u.Recommendation == ClosingRecommendation.CombinationSuggestion),

                TotalSalesValue = units.Sum(u => u.PurchasePrice),
                TotalDepositsPaid = units.Sum(u => u.Deposits.Where(d => d.IsPaid).Sum(d => d.Amount)),
                TotalDiscountExposure = units
                    .Where(u => u.ShortfallAnalysis != null &&
                           u.Recommendation == ClosingRecommendation.CloseWithDiscount)
                    .Sum(u => u.ShortfallAnalysis!.ShortfallAmount),
                TotalVTBExposure = units
                    .Where(u => u.ShortfallAnalysis != null &&
                           (u.Recommendation == ClosingRecommendation.VTBSecondMortgage ||
                            u.Recommendation == ClosingRecommendation.VTBFirstMortgage))
                    .Sum(u => u.ShortfallAnalysis!.ShortfallAmount),
                TotalInvestmentAtRisk = units
                    .Where(u => u.ShortfallAnalysis != null &&
                           (u.Recommendation == ClosingRecommendation.HighRiskDefault ||
                            u.Recommendation == ClosingRecommendation.PotentialDefault))
                    .Sum(u => u.ShortfallAnalysis!.ShortfallAmount),
                TotalIncomeDueClosing = units
                    .Where(u => u.SOA != null)
                    .Sum(u => u.SOA!.BalanceDueOnClosing),
                TotalFundNeededToClose = units
                    .Where(u => u.SOA != null)
                    .Sum(u => u.SOA!.CashRequiredToClose),

                MortgageApprovedCount = units.Count(u => GetPrimaryMortgage(u)?.HasMortgageApproval == true),
                MortgagePendingCount = units.Count(u => GetPrimaryMortgage(u)?.ApprovalType == MortgageApprovalType.PreApproval
                    || GetPrimaryMortgage(u)?.ApprovalType == MortgageApprovalType.Conditional),
                NoMortgageCount = units.Count(u => GetPrimaryMortgage(u) == null || GetPrimaryMortgage(u)?.HasMortgageApproval != true),

                Units = unitItems
            };

            return View(model);
        }

        // ================================================================
        // GET: /Reports/FinancialExposure?projectId=5
        // ================================================================
        public async Task<IActionResult> FinancialExposure(int? projectId)
        {
            var projects = await GetProjectsQuery().ToListAsync();
            if (!projects.Any()) return RedirectToAction("Index");

            var project = projectId.HasValue
                ? projects.FirstOrDefault(p => p.Id == projectId.Value)
                : projects.First();
            if (project == null) return NotFound();

            var units = project.Units.ToList();

            // Categorize by recommendation
            var noExposure = units.Where(u => u.Recommendation == ClosingRecommendation.ProceedToClose || u.Recommendation == null).ToList();
            var discount = units.Where(u => u.Recommendation == ClosingRecommendation.CloseWithDiscount).ToList();
            var vtbSecond = units.Where(u => u.Recommendation == ClosingRecommendation.VTBSecondMortgage).ToList();
            var vtbFirst = units.Where(u => u.Recommendation == ClosingRecommendation.VTBFirstMortgage).ToList();
            var defaultRisk = units.Where(u => u.Recommendation == ClosingRecommendation.HighRiskDefault || u.Recommendation == ClosingRecommendation.PotentialDefault).ToList();

            decimal shortfall(List<Unit> list) => list.Where(u => u.ShortfallAnalysis != null).Sum(u => Math.Max(0, u.ShortfallAnalysis!.ShortfallAmount));
            decimal totalSales = units.Sum(u => u.PurchasePrice);
            decimal discountExp = shortfall(discount);
            decimal vtbSecondExp = shortfall(vtbSecond);
            decimal vtbFirstExp = shortfall(vtbFirst);
            decimal defaultExp = shortfall(defaultRisk);

            var model = new FinancialExposureViewModel
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                ProjectAddress = project.Address,
                AllProjects = projects.Select(p => new ProjectDropdownItem { Id = p.Id, Name = p.Name, UnitCount = p.Units.Count, Status = p.Status }).ToList(),

                NoExposureCount = noExposure.Count,
                DiscountExposureCount = discount.Count,
                VTBSecondCount = vtbSecond.Count,
                VTBFirstCount = vtbFirst.Count,
                DefaultRiskCount = defaultRisk.Count,

                NoExposureValue = 0,
                DiscountExposureValue = discountExp,
                VTBSecondValue = vtbSecondExp,
                VTBFirstValue = vtbFirstExp,
                DefaultRiskValue = defaultExp,

                TotalSalesValue = totalSales,
                BestCaseRecovery = totalSales - discountExp,
                ExpectedRecovery = totalSales - discountExp - (vtbSecondExp * 0.5m) - (vtbFirstExp * 0.3m),
                WorstCaseRecovery = totalSales - discountExp - vtbSecondExp - vtbFirstExp - defaultExp,

                Brackets = new List<ExposureBracket>
                {
                    new() { Label = "0% (Clean)", ColorClass = "success",
                        Count = units.Count(u => GetShortfallPct(u) <= 0),
                        TotalExposure = 0 },
                    new() { Label = "1-10%", ColorClass = "primary",
                        Count = units.Count(u => GetShortfallPct(u) > 0 && GetShortfallPct(u) <= 10),
                        TotalExposure = units.Where(u => GetShortfallPct(u) > 0 && GetShortfallPct(u) <= 10)
                            .Sum(u => u.ShortfallAnalysis?.ShortfallAmount ?? 0) },
                    new() { Label = "10-20%", ColorClass = "warning",
                        Count = units.Count(u => GetShortfallPct(u) > 10 && GetShortfallPct(u) <= 20),
                        TotalExposure = units.Where(u => GetShortfallPct(u) > 10 && GetShortfallPct(u) <= 20)
                            .Sum(u => u.ShortfallAnalysis?.ShortfallAmount ?? 0) },
                    new() { Label = "20-35%", ColorClass = "orange",
                        Count = units.Count(u => GetShortfallPct(u) > 20 && GetShortfallPct(u) <= 35),
                        TotalExposure = units.Where(u => GetShortfallPct(u) > 20 && GetShortfallPct(u) <= 35)
                            .Sum(u => u.ShortfallAnalysis?.ShortfallAmount ?? 0) },
                    new() { Label = "35%+", ColorClass = "danger",
                        Count = units.Count(u => GetShortfallPct(u) > 35),
                        TotalExposure = units.Where(u => GetShortfallPct(u) > 35)
                            .Sum(u => u.ShortfallAnalysis?.ShortfallAmount ?? 0) },
                },

                Units = units.Select(u =>
                {
                    var sa = u.ShortfallAnalysis;
                    var pct = sa?.ShortfallPercentage ?? 0;
                    return new ExposureUnitItem
                    {
                        UnitId = u.Id,
                        UnitNumber = u.UnitNumber,
                        PurchasePrice = u.PurchasePrice,
                        SOAAmount = sa?.SOAAmount ?? 0,
                        TotalFundsAvailable = sa?.TotalFundsAvailable ?? 0,
                        ShortfallAmount = sa?.ShortfallAmount ?? 0,
                        ShortfallPercentage = pct,
                        DepositAtRisk = u.Deposits.Where(d => d.IsPaid).Sum(d => d.Amount),
                        ExposureCategory = GetExposureCategoryLabel(u.Recommendation),
                        CategoryBadgeClass = GetExposureBadgeClass(u.Recommendation),
                        Recommendation = u.Recommendation
                    };
                }).OrderByDescending(u => u.ShortfallAmount).ToList()
            };

            return View(model);
        }

        // ================================================================
        // GET: /Reports/ClosingTimeline?projectId=5
        // ================================================================
        public async Task<IActionResult> ClosingTimeline(int? projectId)
        {
            var projects = await GetProjectsQuery().ToListAsync();
            if (!projects.Any()) return RedirectToAction("Index");

            var project = projectId.HasValue ? projects.FirstOrDefault(p => p.Id == projectId.Value) : projects.First();
            if (project == null) return NotFound();

            var today = DateTime.Today;
            var units = project.Units.ToList();

            TimelineUnitItem MapUnit(Unit u) => new()
            {
                UnitId = u.Id,
                UnitNumber = u.UnitNumber,
                PurchasePrice = u.PurchasePrice,
                ClosingDate = u.ClosingDate ?? u.FirmClosingDate,
                DaysUntilClosing = (u.ClosingDate ?? u.FirmClosingDate).HasValue
                    ? (int)((u.ClosingDate ?? u.FirmClosingDate)!.Value - today).TotalDays : int.MaxValue,
                ShortfallAmount = u.ShortfallAnalysis?.ShortfallAmount ?? 0,
                ShortfallPercentage = u.ShortfallAnalysis?.ShortfallPercentage ?? 0,
                Recommendation = u.Recommendation,
                HasMortgageApproval = GetPrimaryMortgage(u)?.HasMortgageApproval ?? false,
                MortgageProvider = GetPrimaryMortgage(u)?.MortgageProvider,
                PurchaserName = GetPrimaryPurchaserName(u),
                Status = u.Status
            };

            var mapped = units.Select(MapUnit).ToList();

            var overdue = mapped.Where(u => u.ClosingDate.HasValue && u.DaysUntilClosing < 0).OrderBy(u => u.DaysUntilClosing).ToList();
            var thisWeek = mapped.Where(u => u.DaysUntilClosing >= 0 && u.DaysUntilClosing <= 7).OrderBy(u => u.DaysUntilClosing).ToList();
            var thisMonth = mapped.Where(u => u.DaysUntilClosing > 7 && u.DaysUntilClosing <= 30).OrderBy(u => u.DaysUntilClosing).ToList();
            var next3 = mapped.Where(u => u.DaysUntilClosing > 30 && u.DaysUntilClosing <= 90).OrderBy(u => u.DaysUntilClosing).ToList();
            var beyond = mapped.Where(u => u.ClosingDate.HasValue && u.DaysUntilClosing > 90).OrderBy(u => u.DaysUntilClosing).ToList();
            var noDate = mapped.Where(u => !u.ClosingDate.HasValue).ToList();

            TimelineGroup MakeGroup(string label, string icon, string color, List<TimelineUnitItem> items) => new()
            {
                Label = label, Icon = icon, ColorClass = color,
                Count = items.Count,
                TotalValue = items.Sum(u => u.PurchasePrice),
                TotalExposure = items.Where(u => u.ShortfallAmount > 0).Sum(u => u.ShortfallAmount),
                ReadyCount = items.Count(u => u.Recommendation == ClosingRecommendation.ProceedToClose),
                AttentionCount = items.Count(u => u.Recommendation != ClosingRecommendation.ProceedToClose && u.Recommendation != null),
                Units = items
            };

            var model = new ClosingTimelineViewModel
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                ProjectAddress = project.Address,
                AllProjects = projects.Select(p => new ProjectDropdownItem { Id = p.Id, Name = p.Name, UnitCount = p.Units.Count, Status = p.Status }).ToList(),
                OverdueCount = overdue.Count,
                ThisWeekCount = thisWeek.Count,
                ThisMonthCount = thisMonth.Count,
                Next3MonthsCount = next3.Count,
                BeyondCount = beyond.Count,
                NoDateCount = noDate.Count,
                Groups = new List<TimelineGroup>
                {
                    MakeGroup("Overdue", "bi-exclamation-triangle-fill", "danger", overdue),
                    MakeGroup("This Week", "bi-alarm", "orange", thisWeek),
                    MakeGroup("This Month", "bi-calendar-event", "warning", thisMonth),
                    MakeGroup("Next 3 Months", "bi-calendar3", "info", next3),
                    MakeGroup("Beyond 3 Months", "bi-calendar-range", "secondary", beyond),
                    MakeGroup("No Closing Date", "bi-question-circle", "dark", noDate)
                }
            };

            return View(model);
        }

        // ================================================================
        // GET: /Reports/MortgageTracking?projectId=5
        // ================================================================
        public async Task<IActionResult> MortgageTracking(int? projectId)
        {
            var projects = await GetProjectsQuery().ToListAsync();
            if (!projects.Any()) return RedirectToAction("Index");

            var project = projectId.HasValue ? projects.FirstOrDefault(p => p.Id == projectId.Value) : projects.First();
            if (project == null) return NotFound();

            var units = project.Units.ToList();
            var today = DateTime.Today;

            MortgageUnitItem MapUnit(Unit u)
            {
                var mort = GetPrimaryMortgage(u);
                var sa = u.ShortfallAnalysis;
                return new MortgageUnitItem
                {
                    UnitId = u.Id,
                    UnitNumber = u.UnitNumber,
                    PurchasePrice = u.PurchasePrice,
                    ClosingDate = u.ClosingDate ?? u.FirmClosingDate,
                    DaysUntilClosing = (u.ClosingDate ?? u.FirmClosingDate).HasValue
                        ? (int)((u.ClosingDate ?? u.FirmClosingDate)!.Value - today).TotalDays : int.MaxValue,
                    PurchaserName = GetPrimaryPurchaserName(u),
                    PurchaserEmail = GetPrimaryPurchaserEmail(u),
                    HasMortgageApproval = mort?.HasMortgageApproval ?? false,
                    ApprovalType = mort?.ApprovalType ?? MortgageApprovalType.None,
                    MortgageProvider = mort?.MortgageProvider,
                    MortgageAmount = mort?.ApprovedAmount ?? 0,
                    ApprovalExpiryDate = mort?.ApprovalExpiryDate,
                    HasConditions = mort?.HasConditions ?? false,
                    ShortfallAmount = sa?.ShortfallAmount ?? 0,
                    ShortfallPercentage = sa?.ShortfallPercentage ?? 0,
                    LTV = u.PurchasePrice > 0 ? Math.Round((mort?.ApprovedAmount ?? 0) / u.PurchasePrice * 100, 1) : 0,
                    Recommendation = u.Recommendation
                };
            }

            var allMortgageUnits = units.Select(MapUnit).ToList();
            var approved = allMortgageUnits.Where(u => u.HasMortgageApproval &&
                (u.ApprovalType == MortgageApprovalType.FirmApproval || u.ApprovalType == MortgageApprovalType.Blanket)).ToList();

            // Provider breakdown
            var providers = allMortgageUnits
                .Where(u => !string.IsNullOrEmpty(u.MortgageProvider))
                .GroupBy(u => u.MortgageProvider!)
                .Select(g => new MortgageProviderSummary
                {
                    ProviderName = g.Key,
                    UnitCount = g.Count(),
                    TotalAmount = g.Sum(u => u.MortgageAmount),
                    AverageAmount = g.Average(u => u.MortgageAmount),
                    ApprovedCount = g.Count(u => u.HasMortgageApproval),
                    PendingCount = g.Count(u => !u.HasMortgageApproval)
                })
                .OrderByDescending(p => p.UnitCount)
                .ToList();

            // Urgent: closing within 30 days without firm approval
            var urgent = allMortgageUnits
                .Where(u => u.DaysUntilClosing >= 0 && u.DaysUntilClosing <= 30 &&
                       (u.ApprovalType == MortgageApprovalType.None ||
                        u.ApprovalType == MortgageApprovalType.PreApproval ||
                        u.ApprovalType == MortgageApprovalType.Conditional))
                .OrderBy(u => u.DaysUntilClosing)
                .ToList();

            var model = new MortgageTrackingViewModel
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                ProjectAddress = project.Address,
                AllProjects = projects.Select(p => new ProjectDropdownItem { Id = p.Id, Name = p.Name, UnitCount = p.Units.Count, Status = p.Status }).ToList(),
                TotalUnits = units.Count,
                ApprovedCount = allMortgageUnits.Count(u => u.HasMortgageApproval),
                PendingCount = allMortgageUnits.Count(u => u.ApprovalType == MortgageApprovalType.PreApproval || u.ApprovalType == MortgageApprovalType.Conditional),
                NoMortgageCount = allMortgageUnits.Count(u => u.ApprovalType == MortgageApprovalType.None),
                TotalMortgageCommitted = allMortgageUnits.Where(u => u.HasMortgageApproval).Sum(u => u.MortgageAmount),
                AverageLTV = allMortgageUnits.Any(u => u.LTV > 0) ? Math.Round(allMortgageUnits.Where(u => u.LTV > 0).Average(u => u.LTV), 1) : 0,
                Providers = providers,
                UrgentUnits = urgent,
                AllUnits = allMortgageUnits.OrderBy(u => u.ApprovalType).ThenBy(u => u.DaysUntilClosing).ToList()
            };

            return View(model);
        }

        // ================================================================
        // GET: /Reports/DepositTracking?projectId=5
        // ================================================================
        public async Task<IActionResult> DepositTracking(int? projectId)
        {
            var projects = await GetProjectsQuery().ToListAsync();
            if (!projects.Any()) return RedirectToAction("Index");

            var project = projectId.HasValue
                ? projects.FirstOrDefault(p => p.Id == projectId.Value)
                : projects.First();
            if (project == null) return NotFound();

            var units = project.Units.ToList();
            var allDeposits = units.SelectMany(u => u.Deposits).ToList();
            var today = DateTime.Today;

            var unitItems = units.OrderBy(u => u.UnitNumber).Select(u =>
            {
                var deposits = u.Deposits.ToList();
                var paid = deposits.Where(d => d.IsPaid).ToList();
                var pending = deposits.Where(d => !d.IsPaid && d.DueDate >= today).ToList();
                var overdue = deposits.Where(d => !d.IsPaid && d.DueDate < today).ToList();

                return new DepositTrackingUnitItem
                {
                    UnitId = u.Id,
                    UnitNumber = u.UnitNumber,
                    PurchasePrice = u.PurchasePrice,
                    PurchaserName = GetPrimaryPurchaserName(u),
                    TotalDeposits = deposits.Count,
                    PaidDeposits = paid.Count,
                    PendingDeposits = pending.Count,
                    OverdueDeposits = overdue.Count,
                    TotalExpected = deposits.Sum(d => d.Amount),
                    TotalPaid = paid.Sum(d => d.Amount),
                    InterestEarned = deposits.Where(d => d.IsInterestEligible)
                        .SelectMany(d => d.InterestPeriods)
                        .Sum(p =>
                        {
                            var dep = deposits.First(d => d.InterestPeriods.Contains(p));
                            var days = (decimal)(p.PeriodEnd - p.PeriodStart).TotalDays;
                            return dep.Amount * (p.AnnualRate / 100m) * (days / 365m);
                        }),
                    NextDueDate = deposits.Where(d => !d.IsPaid).OrderBy(d => d.DueDate).FirstOrDefault()?.DueDate,
                    HasOverdue = overdue.Any(),
                    Deposits = deposits.OrderBy(d => d.DueDate).Select(d => new DepositLineItem
                    {
                        DepositName = d.DepositName,
                        Amount = d.Amount,
                        DueDate = d.DueDate,
                        PaidDate = d.PaidDate,
                        IsPaid = d.IsPaid,
                        Status = !d.IsPaid && d.DueDate < today ? DepositStatus.Late : d.Status,
                        Holder = d.Holder.ToString()
                    }).ToList()
                };
            }).ToList();

            var paidDeposits = allDeposits.Where(d => d.IsPaid).ToList();

            var model = new DepositTrackingViewModel
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                ProjectAddress = project.Address,
                AllProjects = projects.Select(p => new ProjectDropdownItem
                    { Id = p.Id, Name = p.Name, UnitCount = p.Units.Count, Status = p.Status }).ToList(),
                TotalUnits = units.Count,
                UnitsWithDeposits = units.Count(u => u.Deposits.Any()),
                TotalDepositsExpected = allDeposits.Sum(d => d.Amount),
                TotalDepositsPaid = paidDeposits.Sum(d => d.Amount),
                DepositsPaidCount = paidDeposits.Count,
                DepositsPendingCount = allDeposits.Count(d => !d.IsPaid && d.DueDate >= today),
                DepositsOverdueCount = allDeposits.Count(d => !d.IsPaid && d.DueDate < today),
                TotalInterestEarned = unitItems.Sum(u => u.InterestEarned),
                HeldByBuilder = paidDeposits.Where(d => d.Holder == DepositHolder.Builder).Sum(d => d.Amount),
                HeldInTrust = paidDeposits.Where(d => d.Holder == DepositHolder.Trust).Sum(d => d.Amount),
                HeldByLawyer = paidDeposits.Where(d => d.Holder == DepositHolder.Lawyer).Sum(d => d.Amount),
                Units = unitItems
            };

            return View(model);
        }

        // ================================================================
        // GET: /Reports/PurchaserDirectory?projectId=5
        // ================================================================
        public async Task<IActionResult> PurchaserDirectory(int? projectId)
        {
            var projects = await GetProjectsQuery().ToListAsync();
            if (!projects.Any()) return RedirectToAction("Index");

            var project = projectId.HasValue
                ? projects.FirstOrDefault(p => p.Id == projectId.Value)
                : projects.First();
            if (project == null) return NotFound();

            var units = project.Units.ToList();

            var purchaserItems = units.SelectMany(u => u.Purchasers.Select(up =>
            {
                var mort = up.MortgageInfo;
                var deposits = u.Deposits.ToList();
                return new PurchaserDirectoryItem
                {
                    PurchaserId = up.PurchaserId,
                    FullName = up.Purchaser != null ? $"{up.Purchaser.FirstName} {up.Purchaser.LastName}".Trim() : "Unknown",
                    Email = up.Purchaser?.Email ?? "",
                    Phone = up.Purchaser?.PhoneNumber,
                    UnitId = u.Id,
                    UnitNumber = u.UnitNumber,
                    PurchasePrice = u.PurchasePrice,
                    IsPrimary = up.IsPrimaryPurchaser,
                    HasMortgageApproval = mort?.HasMortgageApproval ?? false,
                    MortgageStatus = mort?.ApprovalType switch
                    {
                        MortgageApprovalType.FirmApproval => "Firm",
                        MortgageApprovalType.PreApproval => "Pre-Approved",
                        MortgageApprovalType.Blanket => "Blanket",
                        MortgageApprovalType.Conditional => "Conditional",
                        _ => "None"
                    },
                    MortgageBadgeClass = mort?.ApprovalType switch
                    {
                        MortgageApprovalType.FirmApproval => "bg-success",
                        MortgageApprovalType.PreApproval => "bg-info",
                        MortgageApprovalType.Blanket => "bg-primary",
                        MortgageApprovalType.Conditional => "bg-warning text-dark",
                        _ => "bg-danger"
                    },
                    MortgageProvider = mort?.MortgageProvider,
                    MortgageAmount = mort?.ApprovedAmount ?? 0,
                    DepositsPaid = deposits.Where(d => d.IsPaid).Sum(d => d.Amount),
                    DepositsExpected = deposits.Sum(d => d.Amount),
                    ShortfallAmount = u.ShortfallAnalysis?.ShortfallAmount ?? 0,
                    Recommendation = u.Recommendation
                };
            })).OrderBy(p => p.UnitNumber).ThenByDescending(p => p.IsPrimary).ToList();

            var model = new PurchaserDirectoryViewModel
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                ProjectAddress = project.Address,
                AllProjects = projects.Select(p => new ProjectDropdownItem
                    { Id = p.Id, Name = p.Name, UnitCount = p.Units.Count, Status = p.Status }).ToList(),
                TotalPurchasers = purchaserItems.Count,
                WithMortgageApproval = purchaserItems.Count(p => p.HasMortgageApproval),
                WithoutMortgage = purchaserItems.Count(p => !p.HasMortgageApproval),
                HighRiskPurchasers = purchaserItems.Count(p =>
                    p.Recommendation == ClosingRecommendation.HighRiskDefault ||
                    p.Recommendation == ClosingRecommendation.PotentialDefault),
                Purchasers = purchaserItems
            };

            return View(model);
        }

        // ================================================================
        // GET: /Reports/AllProjects
        // ================================================================
        public async Task<IActionResult> AllProjects()
        {
            var projects = await GetProjectsQuery().ToListAsync();

            var model = new AllProjectsReportViewModel
            {
                TotalProjects = projects.Count,
                TotalUnits = projects.Sum(p => p.Units.Count),
                TotalClosingClean = projects.Sum(p => p.Units.Count(u => u.Recommendation == ClosingRecommendation.ProceedToClose)),
                TotalNeedSupport = projects.Sum(p => p.Units.Count(u =>
                    u.Recommendation == ClosingRecommendation.CloseWithDiscount ||
                    u.Recommendation == ClosingRecommendation.VTBSecondMortgage ||
                    u.Recommendation == ClosingRecommendation.VTBFirstMortgage)),
                TotalHighRisk = projects.Sum(p => p.Units.Count(u =>
                    u.Recommendation == ClosingRecommendation.HighRiskDefault ||
                    u.Recommendation == ClosingRecommendation.PotentialDefault)),
                TotalSalesValue = projects.Sum(p => p.Units.Sum(u => u.PurchasePrice)),
                TotalExposure = projects.Sum(p => p.Units.Where(u => u.ShortfallAnalysis != null && u.ShortfallAnalysis.ShortfallAmount > 0).Sum(u => u.ShortfallAnalysis!.ShortfallAmount)),

                Projects = projects.Select(p =>
                {
                    var punits = p.Units.ToList();
                    return new ProjectSummaryCard
                    {
                        ProjectId = p.Id,
                        ProjectName = p.Name,
                        ProjectAddress = p.Address,
                        Status = p.Status,
                        ProjectType = p.ProjectType,
                        ClosingDate = p.ClosingDate,
                        TotalUnits = punits.Count,
                        ClosingClean = punits.Count(u => u.Recommendation == ClosingRecommendation.ProceedToClose),
                        NeedSupport = punits.Count(u =>
                            u.Recommendation == ClosingRecommendation.CloseWithDiscount ||
                            u.Recommendation == ClosingRecommendation.VTBSecondMortgage ||
                            u.Recommendation == ClosingRecommendation.VTBFirstMortgage),
                        HighRisk = punits.Count(u =>
                            u.Recommendation == ClosingRecommendation.HighRiskDefault ||
                            u.Recommendation == ClosingRecommendation.PotentialDefault),
                        TotalSalesValue = punits.Sum(u => u.PurchasePrice),
                        DiscountExposure = punits.Where(u => u.ShortfallAnalysis != null && u.Recommendation == ClosingRecommendation.CloseWithDiscount)
                            .Sum(u => u.ShortfallAnalysis!.ShortfallAmount),
                        VTBExposure = punits.Where(u => u.ShortfallAnalysis != null &&
                            (u.Recommendation == ClosingRecommendation.VTBSecondMortgage || u.Recommendation == ClosingRecommendation.VTBFirstMortgage))
                            .Sum(u => u.ShortfallAnalysis!.ShortfallAmount)
                    };
                }).OrderByDescending(p => p.TotalUnits).ToList()
            };

            return View(model);
        }

        // ================================================================
        // GET: /Reports/UnitCards?projectId=5
        // ================================================================
        public async Task<IActionResult> UnitCards(int? projectId)
        {
            var projects = await GetProjectsQuery().ToListAsync();
            if (!projects.Any()) return RedirectToAction("Index");

            var project = projectId.HasValue
                ? projects.FirstOrDefault(p => p.Id == projectId.Value)
                : projects.First();
            if (project == null) return NotFound();

            var unitItems = project.Units.Select(u => BuildReportUnitItem(u)).ToList();

            var model = new ProjectReportViewModel
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                ProjectAddress = project.Address,
                AllProjects = projects.Select(p => new ProjectDropdownItem { Id = p.Id, Name = p.Name, UnitCount = p.Units.Count, Status = p.Status }).ToList(),
                TotalUnits = unitItems.Count,
                Units = unitItems
            };

            return View(model);
        }

        // ================================================================
        // EXPORTS
        // ================================================================

        [HttpGet]
        public async Task<IActionResult> ExportPDF(int projectId)
        {
            var project = await GetProjectById(projectId);
            if (project == null) return NotFound();

            var unitItems = project.Units.Select(u => BuildReportUnitItem(u)).ToList();
            var bytes = _exportService.GenerateProjectReportPDF(project.Name, project.Address, unitItems);
            return File(bytes, "application/pdf", $"PreConHub_{project.Name.Replace(" ", "_")}_Report.pdf");
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(int projectId)
        {
            var project = await GetProjectById(projectId);
            if (project == null) return NotFound();

            var unitItems = project.Units.Select(u => BuildReportUnitItem(u)).ToList();
            var bytes = _exportService.GenerateProjectReportExcel(project.Name, project.Address, unitItems);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"PreConHub_{project.Name.Replace(" ", "_")}_Report.xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> ExportCSV(int projectId)
        {
            var project = await GetProjectById(projectId);
            if (project == null) return NotFound();

            var unitItems = project.Units.Select(u => BuildReportUnitItem(u)).ToList();
            var bytes = _exportService.GenerateProjectReportCSV(unitItems);
            return File(bytes, "text/csv",
                $"PreConHub_{project.Name.Replace(" ", "_")}_Report.csv");
        }

        // ================================================================
        // HELPER METHODS
        // ================================================================

        /// <summary>
        /// Returns projects query with full includes, filtered by role.
        /// Builders see only their projects; Admins see all.
        /// </summary>
        private IQueryable<Project> GetProjectsQuery()
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            var query = _context.Projects
                .Include(p => p.Units).ThenInclude(u => u.ShortfallAnalysis)
                .Include(p => p.Units).ThenInclude(u => u.SOA)
                .Include(p => p.Units).ThenInclude(u => u.Purchasers).ThenInclude(up => up.Purchaser)
                .Include(p => p.Units).ThenInclude(u => u.Purchasers).ThenInclude(up => up.MortgageInfo)
                .Include(p => p.Units).ThenInclude(u => u.Deposits).ThenInclude(d => d.InterestPeriods)
                .AsNoTracking()
                .AsQueryable();

            if (!isAdmin)
                query = query.Where(p => p.BuilderId == userId);

            return query.OrderByDescending(p => p.CreatedAt);
        }

        private async Task<Project?> GetProjectById(int projectId)
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole("Admin");

            var query = _context.Projects
                .Include(p => p.Units).ThenInclude(u => u.ShortfallAnalysis)
                .Include(p => p.Units).ThenInclude(u => u.SOA)
                .Include(p => p.Units).ThenInclude(u => u.Purchasers).ThenInclude(up => up.Purchaser)
                .Include(p => p.Units).ThenInclude(u => u.Purchasers).ThenInclude(up => up.MortgageInfo)
                .Include(p => p.Units).ThenInclude(u => u.Deposits)
                .AsNoTracking()
                .AsQueryable();

            if (!isAdmin)
                query = query.Where(p => p.BuilderId == userId);

            return await query.FirstOrDefaultAsync(p => p.Id == projectId);
        }

        /// <summary>
        /// Gets the primary purchaser's MortgageInfo for a unit.
        /// </summary>
        private static MortgageInfo? GetPrimaryMortgage(Unit u)
        {
            var primary = u.Purchasers.FirstOrDefault(p => p.IsPrimaryPurchaser) ?? u.Purchasers.FirstOrDefault();
            return primary?.MortgageInfo;
        }

        /// <summary>
        /// Gets primary purchaser full name.
        /// </summary>
        private static string? GetPrimaryPurchaserName(Unit u)
        {
            var primary = u.Purchasers.FirstOrDefault(p => p.IsPrimaryPurchaser) ?? u.Purchasers.FirstOrDefault();
            if (primary?.Purchaser == null) return null;
            return $"{primary.Purchaser.FirstName} {primary.Purchaser.LastName}".Trim();
        }

        private static string? GetPrimaryPurchaserEmail(Unit u)
        {
            var primary = u.Purchasers.FirstOrDefault(p => p.IsPrimaryPurchaser) ?? u.Purchasers.FirstOrDefault();
            return primary?.Purchaser?.Email;
        }

        private static decimal GetShortfallPct(Unit u)
        {
            return u.ShortfallAnalysis?.ShortfallPercentage ?? 0;
        }

        private static string GetExposureCategoryLabel(ClosingRecommendation? rec) => rec switch
        {
            ClosingRecommendation.ProceedToClose => "No Exposure",
            ClosingRecommendation.CloseWithDiscount => "Discount",
            ClosingRecommendation.VTBSecondMortgage => "VTB 2nd",
            ClosingRecommendation.VTBFirstMortgage => "VTB 1st",
            ClosingRecommendation.HighRiskDefault => "Default Risk",
            ClosingRecommendation.PotentialDefault => "Default Risk",
            _ => "Pending"
        };

        private static string GetExposureBadgeClass(ClosingRecommendation? rec) => rec switch
        {
            ClosingRecommendation.ProceedToClose => "bg-success",
            ClosingRecommendation.CloseWithDiscount => "bg-primary",
            ClosingRecommendation.VTBSecondMortgage => "bg-warning text-dark",
            ClosingRecommendation.VTBFirstMortgage => "bg-orange",
            ClosingRecommendation.HighRiskDefault => "bg-danger",
            ClosingRecommendation.PotentialDefault => "bg-danger",
            _ => "bg-secondary"
        };

        /// <summary>
        /// Builds a ReportUnitItem from a Unit entity with all related data.
        /// </summary>
        private ReportUnitItem BuildReportUnitItem(Unit u)
        {
            var mort = GetPrimaryMortgage(u);
            var sa = u.ShortfallAnalysis;
            var soa = u.SOA;
            var today = DateTime.Today;
            var closingDate = u.ClosingDate ?? u.FirmClosingDate;

            return new ReportUnitItem
            {
                UnitId = u.Id,
                UnitNumber = u.UnitNumber,
                FloorNumber = u.FloorNumber,
                UnitType = u.UnitType,
                PurchasePrice = u.PurchasePrice,
                CurrentAppraisalValue = u.CurrentAppraisalValue,

                PurchaserName = GetPrimaryPurchaserName(u),
                PurchaserEmail = GetPrimaryPurchaserEmail(u),

                HasMortgageApproval = mort?.HasMortgageApproval ?? false,
                MortgageApprovalType = mort?.ApprovalType ?? MortgageApprovalType.None,
                MortgageProvider = mort?.MortgageProvider,
                MortgageAmount = mort?.ApprovedAmount ?? 0,

                SOAAmount = soa?.TotalDebits ?? 0,
                DepositsPaid = u.Deposits.Where(d => d.IsPaid).Sum(d => d.Amount),
                BalanceDueOnClosing = soa?.BalanceDueOnClosing ?? 0,
                CashRequiredToClose = soa?.CashRequiredToClose ?? 0,

                ShortfallAmount = sa?.ShortfallAmount ?? 0,
                ShortfallPercentage = sa?.ShortfallPercentage ?? 0,
                SuggestedDiscount = sa?.SuggestedDiscount,
                SuggestedVTBAmount = sa?.SuggestedVTBAmount,
                RiskLevel = sa?.RiskLevel ?? RiskLevel.Low,

                Status = u.Status,
                Recommendation = u.Recommendation,
                ClosingDate = closingDate,
                DaysUntilClosing = closingDate.HasValue ? (int)(closingDate.Value - today).TotalDays : int.MaxValue,
                IsConfirmedByLawyer = u.IsConfirmedByLawyer
            };
        }

        // ================================================================
        // GET: /Reports/CreditScoreDistribution?projectId=5
        // ================================================================
        public async Task<IActionResult> CreditScoreDistribution(int? projectId)
        {
            var userId = _userManager.GetUserId(User);
            var projects = await GetAccessibleProjects(userId!);
            var project = projectId.HasValue
                ? projects.FirstOrDefault(p => p.Id == projectId)
                : projects.FirstOrDefault();

            if (project == null) return View("NoProjects");

            var purchasers = await _context.UnitPurchasers
                .Include(up => up.Purchaser)
                .Include(up => up.Unit)
                .Include(up => up.MortgageInfo)
                .Where(up => up.Unit.ProjectId == project.Id && up.IsPrimaryPurchaser)
                .ToListAsync();

            var items = purchasers.Select(up => new CreditScoreItem
            {
                PurchaserName = $"{up.Purchaser.FirstName} {up.Purchaser.LastName}".Trim(),
                UnitNumber = up.Unit.UnitNumber,
                CreditScore = up.MortgageInfo?.CreditScore,
                MortgageApproved = up.MortgageInfo?.HasMortgageApproval ?? false,
                MortgageProvider = up.MortgageInfo?.MortgageProvider
            }).OrderByDescending(i => i.CreditScore ?? 0).ToList();

            var model = new CreditScoreReportViewModel
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                AllProjects = projects.Select(p => new ProjectDropdownItem
                {
                    Id = p.Id, Name = p.Name, UnitCount = p.Units.Count, Status = p.Status
                }).ToList(),
                TotalPurchasers = items.Count,
                ScoresReported = items.Count(i => i.CreditScore.HasValue),
                ScoresNotReported = items.Count(i => !i.CreditScore.HasValue),
                Excellent = items.Count(i => i.CreditScore >= 750),
                Good = items.Count(i => i.CreditScore >= 700 && i.CreditScore < 750),
                Fair = items.Count(i => i.CreditScore >= 600 && i.CreditScore < 700),
                Poor = items.Count(i => i.CreditScore.HasValue && i.CreditScore < 600),
                Items = items
            };

            return View(model);
        }

        // ================================================================
        // GET: /Reports/ExtensionRequestReport?projectId=5
        // ================================================================
        public async Task<IActionResult> ExtensionRequestReport(int? projectId)
        {
            var userId = _userManager.GetUserId(User);
            var projects = await GetAccessibleProjects(userId!);
            var project = projectId.HasValue
                ? projects.FirstOrDefault(p => p.Id == projectId)
                : projects.FirstOrDefault();

            if (project == null) return View("NoProjects");

            var requests = await _context.ClosingExtensionRequests
                .Include(r => r.Unit)
                .Include(r => r.RequestedByPurchaser)
                .Where(r => r.Unit.ProjectId == project.Id)
                .OrderByDescending(r => r.RequestedDate)
                .ToListAsync();

            var approved = requests.Where(r => r.Status == ClosingExtensionStatus.Approved).ToList();

            var model = new ExtensionRequestReportViewModel
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                AllProjects = projects.Select(p => new ProjectDropdownItem
                {
                    Id = p.Id, Name = p.Name, UnitCount = p.Units.Count, Status = p.Status
                }).ToList(),
                TotalRequests = requests.Count,
                Approved = approved.Count,
                Rejected = requests.Count(r => r.Status == ClosingExtensionStatus.Rejected),
                Pending = requests.Count(r => r.Status == ClosingExtensionStatus.Pending),
                AverageExtensionDays = approved.Any() && approved.All(r => r.OriginalClosingDate.HasValue)
                    ? approved.Average(r => (r.RequestedNewClosingDate - r.OriginalClosingDate!.Value).TotalDays)
                    : 0,
                Items = requests.Select(r => new ExtensionReportItem
                {
                    UnitNumber = r.Unit.UnitNumber,
                    PurchaserName = $"{r.RequestedByPurchaser.FirstName} {r.RequestedByPurchaser.LastName}".Trim(),
                    RequestedDate = r.RequestedDate,
                    OriginalClosingDate = r.OriginalClosingDate,
                    RequestedNewClosingDate = r.RequestedNewClosingDate,
                    Status = r.Status,
                    ReviewedAt = r.ReviewedAt
                }).ToList()
            };

            return View(model);
        }

        // ================================================================
        // GET: /Reports/ProjectInvestmentReport?projectId=5
        // ================================================================
        public async Task<IActionResult> ProjectInvestmentReport(int? projectId)
        {
            var userId = _userManager.GetUserId(User);

            // Builder-only (not admin for investment data)
            if (!User.IsInRole("Builder"))
                return Forbid();

            var projects = await GetAccessibleProjects(userId!);
            var project = projectId.HasValue
                ? projects.FirstOrDefault(p => p.Id == projectId)
                : projects.FirstOrDefault();

            if (project == null) return View("NoProjects");

            var financials = await _context.ProjectFinancials
                .FirstOrDefaultAsync(pf => pf.ProjectId == project.Id);

            var units = await _context.Units
                .Include(u => u.ShortfallAnalysis)
                .Where(u => u.ProjectId == project.Id)
                .ToListAsync();

            var model = new ProjectInvestmentReportViewModel
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                AllProjects = projects.Select(p => new ProjectDropdownItem
                {
                    Id = p.Id, Name = p.Name, UnitCount = p.Units.Count, Status = p.Status
                }).ToList(),
                TotalRevenue = financials?.TotalRevenue ?? 0,
                TotalInvestment = financials?.TotalInvestment ?? 0,
                MarketingCost = financials?.MarketingCost ?? 0,
                ProfitAvailable = financials?.ProfitAvailable ?? 0,
                MaxBuilderCapital = financials?.MaxBuilderCapital ?? 0,
                TotalUnits = units.Count,
                UnsoldUnits = units.Count(u => u.Status != UnitStatus.Closed),
                TotalDiscountAllocated = units
                    .Where(u => u.ShortfallAnalysis?.SuggestedDiscount > 0)
                    .Sum(u => u.ShortfallAnalysis!.SuggestedDiscount ?? 0),
                TotalVTBAllocated = units
                    .Where(u => u.ShortfallAnalysis?.SuggestedVTBAmount > 0)
                    .Sum(u => u.ShortfallAnalysis!.SuggestedVTBAmount ?? 0),
                Units = units.Select(u => new InvestmentUnitItem
                {
                    UnitNumber = u.UnitNumber,
                    PurchasePrice = u.PurchasePrice,
                    ShortfallAmount = u.ShortfallAnalysis?.ShortfallAmount ?? 0,
                    SuggestedDiscount = u.ShortfallAnalysis?.SuggestedDiscount ?? 0,
                    SuggestedVTB = u.ShortfallAnalysis?.SuggestedVTBAmount ?? 0,
                    Recommendation = u.Recommendation
                }).ToList()
            };

            return View(model);
        }

        private async Task<List<Project>> GetAccessibleProjects(string userId)
        {
            var query = _context.Projects.Include(p => p.Units).AsQueryable();
            if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
                query = query.Where(p => p.BuilderId == userId);
            return await query.OrderBy(p => p.Name).ToListAsync();
        }
    }
}
