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

        // ── Additional Recommendation Counts ──
        public int UnitsMutualRelease { get; set; }
        public int UnitsCombination { get; set; }
        public decimal TotalFundNeededToClose { get; set; }

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
            ClosingRecommendation.MutualRelease => "Mutual Release",
            ClosingRecommendation.CombinationSuggestion => "Combination",
            _ => "Pending"
        };

        public string StatusColorClass => Recommendation switch
        {
            ClosingRecommendation.ProceedToClose => "success",
            ClosingRecommendation.CloseWithDiscount => "lightgreen",
            ClosingRecommendation.VTBSecondMortgage => "lightyellow",
            ClosingRecommendation.VTBFirstMortgage => "orange",
            ClosingRecommendation.HighRiskDefault => "danger",
            ClosingRecommendation.PotentialDefault => "danger",
            ClosingRecommendation.MutualRelease => "purple",
            ClosingRecommendation.CombinationSuggestion => "combination",
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
            ClosingRecommendation.MutualRelease => "Mutual Release",
            ClosingRecommendation.CombinationSuggestion => "Combination",
            _ => "Pending Analysis"
        };

        public string StatusColor => Recommendation switch
        {
            ClosingRecommendation.ProceedToClose => "#198754",
            ClosingRecommendation.CloseWithDiscount => "#90EE90",
            ClosingRecommendation.VTBSecondMortgage => "#FFFACD",
            ClosingRecommendation.VTBFirstMortgage => "#FFA500",
            ClosingRecommendation.HighRiskDefault => "#DC3545",
            ClosingRecommendation.PotentialDefault => "#DC3545",
            ClosingRecommendation.MutualRelease => "#9B59B6",
            ClosingRecommendation.CombinationSuggestion => "#F1C40F",
            _ => "#6c757d"
        };

        public string StatusBadgeClass => Recommendation switch
        {
            ClosingRecommendation.ProceedToClose => "bg-success",
            ClosingRecommendation.CloseWithDiscount => "bg-lightgreen",
            ClosingRecommendation.VTBSecondMortgage => "bg-lightyellow",
            ClosingRecommendation.VTBFirstMortgage => "bg-orange",
            ClosingRecommendation.HighRiskDefault => "bg-danger",
            ClosingRecommendation.PotentialDefault => "bg-danger",
            ClosingRecommendation.MutualRelease => "bg-purple",
            ClosingRecommendation.CombinationSuggestion => "bg-combination",
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

    // ============================================================
    // DEPOSIT TRACKING REPORT
    // ============================================================

    public class DepositTrackingViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public string ProjectAddress { get; set; } = "";
        public List<ProjectDropdownItem> AllProjects { get; set; } = new();

        // ── Summary ──
        public int TotalUnits { get; set; }
        public int UnitsWithDeposits { get; set; }
        public decimal TotalDepositsExpected { get; set; }
        public decimal TotalDepositsPaid { get; set; }
        public decimal TotalDepositsOutstanding => TotalDepositsExpected - TotalDepositsPaid;
        public decimal CollectionRate => TotalDepositsExpected > 0
            ? Math.Round(TotalDepositsPaid / TotalDepositsExpected * 100, 1) : 0;

        public int DepositsPaidCount { get; set; }
        public int DepositsPendingCount { get; set; }
        public int DepositsOverdueCount { get; set; }
        public decimal TotalInterestEarned { get; set; }

        // ── Holder Breakdown ──
        public decimal HeldByBuilder { get; set; }
        public decimal HeldInTrust { get; set; }
        public decimal HeldByLawyer { get; set; }

        // ── Unit Details ──
        public List<DepositTrackingUnitItem> Units { get; set; } = new();
    }

    public class DepositTrackingUnitItem
    {
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = "";
        public decimal PurchasePrice { get; set; }
        public string? PurchaserName { get; set; }

        public int TotalDeposits { get; set; }
        public int PaidDeposits { get; set; }
        public int PendingDeposits { get; set; }
        public int OverdueDeposits { get; set; }

        public decimal TotalExpected { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal Outstanding => TotalExpected - TotalPaid;
        public decimal DepositPercent => PurchasePrice > 0
            ? Math.Round(TotalPaid / PurchasePrice * 100, 1) : 0;

        public decimal InterestEarned { get; set; }
        public DateTime? NextDueDate { get; set; }
        public bool HasOverdue { get; set; }

        public List<DepositLineItem> Deposits { get; set; } = new();
    }

    public class DepositLineItem
    {
        public string DepositName { get; set; } = "";
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? PaidDate { get; set; }
        public bool IsPaid { get; set; }
        public DepositStatus Status { get; set; }
        public string Holder { get; set; } = "";

        public string StatusBadgeClass => Status switch
        {
            DepositStatus.Paid => "bg-success",
            DepositStatus.Pending => DueDate < DateTime.Today ? "bg-danger" : "bg-warning text-dark",
            DepositStatus.Late => "bg-danger",
            DepositStatus.Refunded => "bg-info",
            DepositStatus.Forfeited => "bg-dark",
            _ => "bg-secondary"
        };

        public string StatusText => Status switch
        {
            DepositStatus.Paid => "Paid",
            DepositStatus.Pending => DueDate < DateTime.Today ? "Overdue" : "Pending",
            DepositStatus.Late => "Late",
            DepositStatus.Refunded => "Refunded",
            DepositStatus.Forfeited => "Forfeited",
            _ => "Unknown"
        };
    }

    // ============================================================
    // PURCHASER DIRECTORY REPORT
    // ============================================================

    public class PurchaserDirectoryViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public string ProjectAddress { get; set; } = "";
        public List<ProjectDropdownItem> AllProjects { get; set; } = new();

        // ── Summary ──
        public int TotalPurchasers { get; set; }
        public int WithMortgageApproval { get; set; }
        public int WithoutMortgage { get; set; }
        public int HighRiskPurchasers { get; set; }

        // ── Purchaser List ──
        public List<PurchaserDirectoryItem> Purchasers { get; set; } = new();
    }

    public class PurchaserDirectoryItem
    {
        public string PurchaserId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Phone { get; set; }

        // Unit info
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = "";
        public decimal PurchasePrice { get; set; }
        public bool IsPrimary { get; set; }

        // Financial readiness
        public bool HasMortgageApproval { get; set; }
        public string MortgageStatus { get; set; } = "";
        public string MortgageBadgeClass { get; set; } = "";
        public string? MortgageProvider { get; set; }
        public decimal MortgageAmount { get; set; }

        public decimal DepositsPaid { get; set; }
        public decimal DepositsExpected { get; set; }
        public decimal DepositPercent => DepositsExpected > 0
            ? Math.Round(DepositsPaid / DepositsExpected * 100, 1) : 0;

        public decimal ShortfallAmount { get; set; }
        public ClosingRecommendation? Recommendation { get; set; }

        public string RecommendationText => Recommendation switch
        {
            ClosingRecommendation.ProceedToClose => "Proceed to Close",
            ClosingRecommendation.CloseWithDiscount => "Close with Discount",
            ClosingRecommendation.VTBSecondMortgage => "VTB 2nd Mortgage",
            ClosingRecommendation.VTBFirstMortgage => "VTB 1st Mortgage",
            ClosingRecommendation.HighRiskDefault => "High Risk",
            ClosingRecommendation.PotentialDefault => "Potential Default",
            ClosingRecommendation.MutualRelease => "Mutual Release",
            ClosingRecommendation.CombinationSuggestion => "Combination",
            _ => "Pending"
        };

        public string RecommendationBadgeClass => Recommendation switch
        {
            ClosingRecommendation.ProceedToClose => "bg-success",
            ClosingRecommendation.CloseWithDiscount => "bg-lightgreen",
            ClosingRecommendation.VTBSecondMortgage => "bg-lightyellow",
            ClosingRecommendation.VTBFirstMortgage => "bg-orange",
            ClosingRecommendation.HighRiskDefault => "bg-danger",
            ClosingRecommendation.PotentialDefault => "bg-danger",
            ClosingRecommendation.MutualRelease => "bg-purple",
            ClosingRecommendation.CombinationSuggestion => "bg-combination",
            _ => "bg-secondary"
        };
    }

    // ============================================================
    // CREDIT SCORE DISTRIBUTION REPORT
    // ============================================================
    public class CreditScoreReportViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public List<ProjectDropdownItem> AllProjects { get; set; } = new();

        public int TotalPurchasers { get; set; }
        public int ScoresReported { get; set; }
        public int ScoresNotReported { get; set; }

        // Bands
        public int Excellent { get; set; } // 750+
        public int Good { get; set; }      // 700-749
        public int Fair { get; set; }      // 600-699
        public int Poor { get; set; }      // <600

        public List<CreditScoreItem> Items { get; set; } = new();
    }

    public class CreditScoreItem
    {
        public string PurchaserName { get; set; } = "";
        public string UnitNumber { get; set; } = "";
        public int? CreditScore { get; set; }
        public bool MortgageApproved { get; set; }
        public string? MortgageProvider { get; set; }
        public string Band => CreditScore switch
        {
            >= 750 => "Excellent",
            >= 700 => "Good",
            >= 600 => "Fair",
            _ when CreditScore.HasValue => "Poor",
            _ => "Not Reported"
        };
        public string BandBadgeClass => CreditScore switch
        {
            >= 750 => "bg-success",
            >= 700 => "bg-info",
            >= 600 => "bg-warning text-dark",
            _ when CreditScore.HasValue => "bg-danger",
            _ => "bg-secondary"
        };
    }

    // ============================================================
    // EXTENSION REQUEST REPORT
    // ============================================================
    public class ExtensionRequestReportViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public List<ProjectDropdownItem> AllProjects { get; set; } = new();

        public int TotalRequests { get; set; }
        public int Approved { get; set; }
        public int Rejected { get; set; }
        public int Pending { get; set; }
        public double ApprovalRate => TotalRequests > 0 ? Math.Round((double)Approved / TotalRequests * 100, 1) : 0;
        public double AverageExtensionDays { get; set; }

        public List<ExtensionReportItem> Items { get; set; } = new();
    }

    public class ExtensionReportItem
    {
        public string UnitNumber { get; set; } = "";
        public string PurchaserName { get; set; } = "";
        public DateTime RequestedDate { get; set; }
        public DateTime? OriginalClosingDate { get; set; }
        public DateTime RequestedNewClosingDate { get; set; }
        public ClosingExtensionStatus Status { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public int DaysRequested => OriginalClosingDate.HasValue
            ? (int)(RequestedNewClosingDate - OriginalClosingDate.Value).TotalDays : 0;
        public string StatusBadgeClass => Status switch
        {
            ClosingExtensionStatus.Pending => "bg-warning text-dark",
            ClosingExtensionStatus.Approved => "bg-success",
            ClosingExtensionStatus.Rejected => "bg-danger",
            _ => "bg-secondary"
        };
    }

    // ============================================================
    // PROJECT INVESTMENT REPORT (Builder-only)
    // ============================================================
    public class ProjectInvestmentReportViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public List<ProjectDropdownItem> AllProjects { get; set; } = new();

        public decimal TotalRevenue { get; set; }
        public decimal TotalInvestment { get; set; }
        public decimal MarketingCost { get; set; }
        public decimal ProfitAvailable { get; set; }
        public decimal MaxBuilderCapital { get; set; }
        public int TotalUnits { get; set; }
        public int UnsoldUnits { get; set; }
        public decimal ProfitPerUnit => UnsoldUnits > 0 ? ProfitAvailable / UnsoldUnits : 0;
        public decimal VTBPerUnit => UnsoldUnits > 0 ? MaxBuilderCapital / UnsoldUnits : 0;

        public decimal TotalDiscountAllocated { get; set; }
        public decimal TotalVTBAllocated { get; set; }

        public List<InvestmentUnitItem> Units { get; set; } = new();
    }

    public class InvestmentUnitItem
    {
        public string UnitNumber { get; set; } = "";
        public decimal PurchasePrice { get; set; }
        public decimal ShortfallAmount { get; set; }
        public decimal SuggestedDiscount { get; set; }
        public decimal SuggestedVTB { get; set; }
        public ClosingRecommendation? Recommendation { get; set; }
        public string RecommendationText => Recommendation switch
        {
            ClosingRecommendation.ProceedToClose => "Proceed",
            ClosingRecommendation.CloseWithDiscount => "Discount",
            ClosingRecommendation.VTBSecondMortgage => "VTB 2nd",
            ClosingRecommendation.VTBFirstMortgage => "VTB 1st",
            ClosingRecommendation.HighRiskDefault => "Default Risk",
            ClosingRecommendation.MutualRelease => "Mutual Release",
            ClosingRecommendation.CombinationSuggestion => "Combination",
            _ => "Pending"
        };
    }
}
