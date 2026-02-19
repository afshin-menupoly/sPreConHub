using PreConHub.Models.Entities;

namespace PreConHub.Models.ViewModels
{
    // ============================================================
    // REPORTS HUB (Landing Page)
    // ============================================================

    public class ReportsHubViewModel
    {
        // Quick Stats
        public int TotalProjects { get; set; }
        public int TotalUnits { get; set; }
        public int UnitsClosingClean { get; set; }
        public int UnitsAtRisk { get; set; }
        public decimal TotalSalesValue { get; set; }
        public decimal TotalExposure { get; set; }

        // Project selector
        public List<ProjectDropdownItem> Projects { get; set; } = new();
    }

    public class ProjectDropdownItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int UnitCount { get; set; }
        public ProjectStatus Status { get; set; }
    }

    // ============================================================
    // PROJECT REPORT (Enhanced Dashboard per Project)
    // ============================================================

    public class ProjectReportViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public string ProjectAddress { get; set; } = "";
        public string City { get; set; } = "";
        public ProjectStatus ProjectStatus { get; set; }
        public ProjectType ProjectType { get; set; }
        public DateTime? ClosingDate { get; set; }
        public DateTime? OccupancyDate { get; set; }
        public string? BuilderCompanyName { get; set; }

        // Project Selector for switching between projects
        public List<ProjectDropdownItem> AllProjects { get; set; } = new();

        // ── Summary Metrics ──
        public int TotalUnits { get; set; }
        public int UnitsReadyToClose { get; set; }
        public int UnitsNeedingDiscount { get; set; }
        public int UnitsNeedingVTB { get; set; }
        public int UnitsAtRisk { get; set; }
        public int UnitsPendingData { get; set; }

        public decimal PercentReadyToClose => TotalUnits > 0
            ? Math.Round((decimal)UnitsReadyToClose / TotalUnits * 100, 1) : 0;
        public decimal PercentNeedingDiscount => TotalUnits > 0
            ? Math.Round((decimal)UnitsNeedingDiscount / TotalUnits * 100, 1) : 0;
        public decimal PercentNeedingVTB => TotalUnits > 0
            ? Math.Round((decimal)UnitsNeedingVTB / TotalUnits * 100, 1) : 0;
        public decimal PercentAtRisk => TotalUnits > 0
            ? Math.Round((decimal)UnitsAtRisk / TotalUnits * 100, 1) : 0;

        // ── Financial Metrics ──
        public decimal TotalSalesValue { get; set; }
        public decimal TotalDepositsPaid { get; set; }
        public decimal TotalDiscountExposure { get; set; }
        public decimal TotalVTBExposure { get; set; }
        public decimal TotalInvestmentAtRisk { get; set; }
        public decimal TotalIncomeDueClosing { get; set; }

        public decimal CapitalRecoveryPercent => TotalUnits > 0
            ? Math.Round((decimal)(UnitsReadyToClose + UnitsNeedingDiscount) / TotalUnits * 100, 1) : 0;
        public decimal ClosingProbabilityPercent => TotalUnits > 0
            ? Math.Round((decimal)(TotalUnits - UnitsAtRisk) / TotalUnits * 100, 1) : 0;
        public decimal LitigationRiskPercent => TotalUnits > 0
            ? Math.Round((decimal)UnitsAtRisk / TotalUnits * 100, 1) : 0;
        public decimal DiscountPercentOfSales => TotalSalesValue > 0
            ? Math.Round(TotalDiscountExposure / TotalSalesValue * 100, 2) : 0;

        // ── Mortgage Summary ──
        public int MortgageApprovedCount { get; set; }
        public int MortgagePendingCount { get; set; }
        public int NoMortgageCount { get; set; }

        // ── Unit Details ──
        public List<ReportUnitItem> Units { get; set; } = new();
    }

    // ============================================================
    // FINANCIAL EXPOSURE REPORT
    // ============================================================

    public class FinancialExposureViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public string ProjectAddress { get; set; } = "";
        public List<ProjectDropdownItem> AllProjects { get; set; } = new();

        // ── Exposure Categories ──
        public int NoExposureCount { get; set; }
        public int DiscountExposureCount { get; set; }
        public int VTBSecondCount { get; set; }
        public int VTBFirstCount { get; set; }
        public int DefaultRiskCount { get; set; }

        public decimal NoExposureValue { get; set; }
        public decimal DiscountExposureValue { get; set; }
        public decimal VTBSecondValue { get; set; }
        public decimal VTBFirstValue { get; set; }
        public decimal DefaultRiskValue { get; set; }

        public decimal TotalExposure => DiscountExposureValue + VTBSecondValue + VTBFirstValue + DefaultRiskValue;
        public int TotalUnits => NoExposureCount + DiscountExposureCount + VTBSecondCount + VTBFirstCount + DefaultRiskCount;

        // ── Scenario Analysis ──
        public decimal TotalSalesValue { get; set; }
        public decimal BestCaseRecovery { get; set; }   // All close with discounts only
        public decimal ExpectedRecovery { get; set; }    // Weighted probability
        public decimal WorstCaseRecovery { get; set; }   // All shortfalls realized

        // ── Exposure Brackets ──
        public List<ExposureBracket> Brackets { get; set; } = new();

        // ── Unit Details ──
        public List<ExposureUnitItem> Units { get; set; } = new();
    }

    public class ExposureBracket
    {
        public string Label { get; set; } = "";       // e.g., "0%", "1-10%", "10-20%", "20-35%", "35%+"
        public string ColorClass { get; set; } = "";
        public int Count { get; set; }
        public decimal TotalExposure { get; set; }
    }

    public class ExposureUnitItem
    {
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = "";
        public decimal PurchasePrice { get; set; }
        public decimal SOAAmount { get; set; }
        public decimal TotalFundsAvailable { get; set; }
        public decimal ShortfallAmount { get; set; }
        public decimal ShortfallPercentage { get; set; }
        public decimal DepositAtRisk { get; set; }
        public string ExposureCategory { get; set; } = "";
        public string CategoryBadgeClass { get; set; } = "";
        public ClosingRecommendation? Recommendation { get; set; }
    }

    // ============================================================
    // CLOSING TIMELINE REPORT
    // ============================================================

    public class ClosingTimelineViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public string ProjectAddress { get; set; } = "";
        public List<ProjectDropdownItem> AllProjects { get; set; } = new();

        // ── Timeline Groups ──
        public List<TimelineGroup> Groups { get; set; } = new();

        // ── Summary Cards ──
        public int OverdueCount { get; set; }
        public int ThisWeekCount { get; set; }
        public int ThisMonthCount { get; set; }
        public int Next3MonthsCount { get; set; }
        public int BeyondCount { get; set; }
        public int NoDateCount { get; set; }
    }

    public class TimelineGroup
    {
        public string Label { get; set; } = "";           // "Overdue", "This Week", etc.
        public string Icon { get; set; } = "";
        public string ColorClass { get; set; } = "";
        public int Count { get; set; }
        public decimal TotalValue { get; set; }
        public decimal TotalExposure { get; set; }
        public int ReadyCount { get; set; }
        public int AttentionCount { get; set; }
        public List<TimelineUnitItem> Units { get; set; } = new();
    }

    public class TimelineUnitItem
    {
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = "";
        public decimal PurchasePrice { get; set; }
        public DateTime? ClosingDate { get; set; }
        public int DaysUntilClosing { get; set; }
        public decimal ShortfallAmount { get; set; }
        public decimal ShortfallPercentage { get; set; }
        public ClosingRecommendation? Recommendation { get; set; }
        public bool HasMortgageApproval { get; set; }
        public string? MortgageProvider { get; set; }
        public string? PurchaserName { get; set; }
        public UnitStatus Status { get; set; }

        public string RecommendationText => Recommendation switch
        {
            ClosingRecommendation.ProceedToClose => "Proceed to Close",
            ClosingRecommendation.CloseWithDiscount => "Close with Discount",
            ClosingRecommendation.VTBSecondMortgage => "VTB 2nd Mortgage",
            ClosingRecommendation.VTBFirstMortgage => "VTB 1st Mortgage",
            ClosingRecommendation.HighRiskDefault => "High Risk",
            ClosingRecommendation.PotentialDefault => "Potential Default",
            _ => "Pending"
        };

        public string StatusColorClass => Recommendation switch
        {
            ClosingRecommendation.ProceedToClose => "success",
            ClosingRecommendation.CloseWithDiscount => "primary",
            ClosingRecommendation.VTBSecondMortgage => "warning",
            ClosingRecommendation.VTBFirstMortgage => "orange",
            ClosingRecommendation.HighRiskDefault => "danger",
            ClosingRecommendation.PotentialDefault => "danger",
            _ => "secondary"
        };
    }

    // ============================================================
    // MORTGAGE TRACKING REPORT
    // ============================================================

    public class MortgageTrackingViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public string ProjectAddress { get; set; } = "";
        public List<ProjectDropdownItem> AllProjects { get; set; } = new();

        // ── Summary ──
        public int TotalUnits { get; set; }
        public int ApprovedCount { get; set; }
        public int PendingCount { get; set; }
        public int NoMortgageCount { get; set; }
        public decimal TotalMortgageCommitted { get; set; }
        public decimal AverageLTV { get; set; }

        // ── Provider Breakdown ──
        public List<MortgageProviderSummary> Providers { get; set; } = new();

        // ── Urgent Attention (closing within 30 days, no approval) ──
        public List<MortgageUnitItem> UrgentUnits { get; set; } = new();

        // ── All Units ──
        public List<MortgageUnitItem> AllUnits { get; set; } = new();
    }

    public class MortgageProviderSummary
    {
        public string ProviderName { get; set; } = "";
        public int UnitCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AverageAmount { get; set; }
        public int ApprovedCount { get; set; }
        public int PendingCount { get; set; }
    }

    public class MortgageUnitItem
    {
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = "";
        public decimal PurchasePrice { get; set; }
        public DateTime? ClosingDate { get; set; }
        public int DaysUntilClosing { get; set; }
        public string? PurchaserName { get; set; }
        public string? PurchaserEmail { get; set; }

        // Mortgage
        public bool HasMortgageApproval { get; set; }
        public MortgageApprovalType ApprovalType { get; set; }
        public string? MortgageProvider { get; set; }
        public decimal MortgageAmount { get; set; }
        public DateTime? ApprovalExpiryDate { get; set; }
        public bool HasConditions { get; set; }

        // Financials
        public decimal ShortfallAmount { get; set; }
        public decimal ShortfallPercentage { get; set; }
        public decimal LTV { get; set; }  // MortgageAmount / PurchasePrice * 100
        public ClosingRecommendation? Recommendation { get; set; }

        public string ApprovalStatusText => ApprovalType switch
        {
            MortgageApprovalType.FirmApproval => "Firm",
            MortgageApprovalType.PreApproval => "Pre-Approved",
            MortgageApprovalType.Blanket => "Blanket",
            MortgageApprovalType.Conditional => "Conditional",
            _ => "None"
        };

        public string ApprovalBadgeClass => ApprovalType switch
        {
            MortgageApprovalType.FirmApproval => "bg-success",
            MortgageApprovalType.PreApproval => "bg-info",
            MortgageApprovalType.Blanket => "bg-primary",
            MortgageApprovalType.Conditional => "bg-warning text-dark",
            _ => "bg-danger"
        };
    }

    // ============================================================
    // ALL PROJECTS OVERVIEW (Multi-Project Comparison)
    // ============================================================

    public class AllProjectsReportViewModel
    {
        // ── Aggregate ──
        public int TotalProjects { get; set; }
        public int TotalUnits { get; set; }
        public int TotalClosingClean { get; set; }
        public int TotalNeedSupport { get; set; }
        public int TotalHighRisk { get; set; }
        public decimal TotalSalesValue { get; set; }
        public decimal TotalExposure { get; set; }

        // ── Per-Project Summaries ──
        public List<ProjectSummaryCard> Projects { get; set; } = new();
    }

    public class ProjectSummaryCard
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public string ProjectAddress { get; set; } = "";
        public ProjectStatus Status { get; set; }
        public ProjectType ProjectType { get; set; }
        public DateTime? ClosingDate { get; set; }

        public int TotalUnits { get; set; }
        public int ClosingClean { get; set; }
        public int NeedSupport { get; set; }
        public int HighRisk { get; set; }

        public decimal TotalSalesValue { get; set; }
        public decimal DiscountExposure { get; set; }
        public decimal VTBExposure { get; set; }
        public decimal TotalExposure => DiscountExposure + VTBExposure;

        public decimal CapitalRecoveryPercent => TotalUnits > 0
            ? Math.Round((decimal)ClosingClean / TotalUnits * 100, 1) : 0;

        public string HealthStatus => CapitalRecoveryPercent >= 70 ? "On Track"
            : CapitalRecoveryPercent >= 40 ? "At Risk" : "Critical";

        public string HealthBadgeClass => CapitalRecoveryPercent >= 70 ? "bg-success"
            : CapitalRecoveryPercent >= 40 ? "bg-warning text-dark" : "bg-danger";
    }

    // ============================================================
    // SHARED: REPORT UNIT ITEM (used in ProjectReport, Exports)
    // ============================================================

    public class ReportUnitItem
    {
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = "";
        public string? FloorNumber { get; set; }
        public UnitType UnitType { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal? CurrentAppraisalValue { get; set; }

        // Purchaser
        public string? PurchaserName { get; set; }
        public string? PurchaserEmail { get; set; }

        // Mortgage
        public bool HasMortgageApproval { get; set; }
        public MortgageApprovalType MortgageApprovalType { get; set; }
        public string? MortgageProvider { get; set; }
        public decimal MortgageAmount { get; set; }

        // SOA
        public decimal SOAAmount { get; set; }
        public decimal DepositsPaid { get; set; }
        public decimal BalanceDueOnClosing { get; set; }
        public decimal CashRequiredToClose { get; set; }

        // Shortfall
        public decimal ShortfallAmount { get; set; }
        public decimal ShortfallPercentage { get; set; }
        public decimal? SuggestedDiscount { get; set; }
        public decimal? SuggestedVTBAmount { get; set; }
        public RiskLevel RiskLevel { get; set; }

        // Status
        public UnitStatus Status { get; set; }
        public ClosingRecommendation? Recommendation { get; set; }
        public DateTime? ClosingDate { get; set; }
        public int DaysUntilClosing { get; set; }

        // Lawyer
        public bool IsConfirmedByLawyer { get; set; }

        // Computed display helpers
        public string RecommendationText => Recommendation switch
        {
            ClosingRecommendation.ProceedToClose => "Proceed to Close",
            ClosingRecommendation.CloseWithDiscount => "Close with Discount",
            ClosingRecommendation.VTBSecondMortgage => "VTB 2nd Mortgage",
            ClosingRecommendation.VTBFirstMortgage => "VTB 1st Mortgage",
            ClosingRecommendation.HighRiskDefault => "High Risk",
            ClosingRecommendation.PotentialDefault => "Potential Default",
            _ => "Pending Analysis"
        };

        public string StatusColor => Recommendation switch
        {
            ClosingRecommendation.ProceedToClose => "#28a745",
            ClosingRecommendation.CloseWithDiscount => "#007bff",
            ClosingRecommendation.VTBSecondMortgage => "#ffc107",
            ClosingRecommendation.VTBFirstMortgage => "#f97316",
            ClosingRecommendation.HighRiskDefault => "#dc3545",
            ClosingRecommendation.PotentialDefault => "#8b0000",
            _ => "#6c757d"
        };

        public string StatusBadgeClass => Recommendation switch
        {
            ClosingRecommendation.ProceedToClose => "bg-success",
            ClosingRecommendation.CloseWithDiscount => "bg-primary",
            ClosingRecommendation.VTBSecondMortgage => "bg-warning text-dark",
            ClosingRecommendation.VTBFirstMortgage => "bg-orange",
            ClosingRecommendation.HighRiskDefault => "bg-danger",
            ClosingRecommendation.PotentialDefault => "bg-danger",
            _ => "bg-secondary"
        };

        public string MortgageStatusText => MortgageApprovalType switch
        {
            MortgageApprovalType.FirmApproval => "Firm",
            MortgageApprovalType.PreApproval => "Pre-Approved",
            MortgageApprovalType.Blanket => "Blanket",
            MortgageApprovalType.Conditional => "Conditional",
            _ => HasMortgageApproval ? "Yes" : "None"
        };

        public string MortgageBadgeClass => MortgageApprovalType switch
        {
            MortgageApprovalType.FirmApproval => "bg-success",
            MortgageApprovalType.PreApproval => "bg-info",
            MortgageApprovalType.Blanket => "bg-primary",
            MortgageApprovalType.Conditional => "bg-warning text-dark",
            _ => "bg-danger"
        };
    }
}
