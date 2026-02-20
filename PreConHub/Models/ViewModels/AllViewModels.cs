using PreConHub.Models.Entities;
using PreConHub.Services;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PreConHub.Models.ViewModels
{
    #region Project Dashboard

    public class ProjectDashboardViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string ProjectAddress { get; set; } = string.Empty;
        public ProjectSummaryViewModel Summary { get; set; } = new();
        public List<UnitRowViewModel> Units { get; set; } = new();
        public decimal TotalIncomeDueClosing { get; set; }
        public int? MaxUnits { get; set; }
        public int CurrentUnitCount { get; set; }
        public bool CanAddUnit { get; set; }
    }

    public class ProjectSummaryViewModel
    {
        public int TotalUnits { get; set; }
        public int UnitsReadyToClose { get; set; }
        public int UnitsNeedingDiscount { get; set; }
        public int UnitsNeedingVTB { get; set; }
        public int UnitsAtRisk { get; set; }
        public int UnitsPendingData { get; set; }
        
        public decimal PercentReadyToClose { get; set; }
        public decimal PercentNeedingDiscount { get; set; }
        public decimal PercentNeedingVTB { get; set; }
        public decimal PercentAtRisk { get; set; }
        
        public decimal TotalSalesValue { get; set; }
        public decimal TotalDiscountRequired { get; set; }
        public decimal DiscountPercentOfSales { get; set; }
        public decimal TotalInvestmentAtRisk { get; set; }
        public decimal TotalShortfall { get; set; }
        public decimal ClosingProbabilityPercent { get; set; }
    }

    public class UnitRowViewModel
    {
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = string.Empty;
        
        // Mortgage Info
        public bool HasMortgageApproval { get; set; }
        public string? MortgageProvider { get; set; }
        public decimal MortgageAmount { get; set; }
        public bool IsApprovedAtClosing { get; set; }
        public decimal? AppraisalValue { get; set; }
        
        // Financial
        public decimal SOAAmount { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal ShortfallAmount { get; set; }
        public decimal ShortfallPercent { get; set; }
        
        // Recommendation
        public ClosingRecommendation Recommendation { get; set; }
        
        // Computed display properties
        public string RecommendationText => Recommendation switch
        {
            ClosingRecommendation.ProceedToClose => "Proceed to Close",
            ClosingRecommendation.CloseWithDiscount => "Close with Discount",
            ClosingRecommendation.VTBSecondMortgage => "VTB Second Mortgage",
            ClosingRecommendation.VTBFirstMortgage => "VTB First Mortgage",
            ClosingRecommendation.HighRiskDefault => "High Risk of Default",
            ClosingRecommendation.PotentialDefault => "Potential Default",
            ClosingRecommendation.MutualRelease => "Mutual Release",
            ClosingRecommendation.CombinationSuggestion => "Combination",
            _ => "Pending"
        };

        public string RecommendationClass => Recommendation switch
        {
            ClosingRecommendation.ProceedToClose => "rec-proceed",
            ClosingRecommendation.CloseWithDiscount => "rec-discount",
            ClosingRecommendation.VTBSecondMortgage => "rec-vtb-second",
            ClosingRecommendation.VTBFirstMortgage => "rec-vtb-first",
            ClosingRecommendation.HighRiskDefault => "rec-default",
            ClosingRecommendation.PotentialDefault => "rec-default",
            ClosingRecommendation.MutualRelease => "rec-mutual",
            ClosingRecommendation.CombinationSuggestion => "rec-combination",
            _ => ""
        };
        
        public string ShortfallPercentClass
        {
            get
            {
                if (ShortfallPercent <= 0) return "shortfall-low";
                if (ShortfallPercent < 10) return "shortfall-low";
                if (ShortfallPercent < 20) return "shortfall-medium";
                if (ShortfallPercent < 30) return "shortfall-high";
                return "shortfall-critical";
            }
        }
    }

    #endregion

    #region Project List

    public class ProjectListViewModel
    {
        public List<ProjectItemViewModel> Projects { get; set; } = new();
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public int MaxProjects { get; set; }
        public bool CanCreateProject { get; set; } = true;
    }

    public class ProjectItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public int TotalUnits { get; set; }
        public ProjectStatus Status { get; set; }
        public DateTime? ClosingDate { get; set; }
        
        // Quick stats
        public int UnitsReadyToClose { get; set; }
        public int UnitsAtRisk { get; set; }
        public decimal ClosingProbability { get; set; }
        
        public string StatusBadgeClass => Status switch
        {
            ProjectStatus.Active => "bg-success",
            ProjectStatus.Closing => "bg-warning",
            ProjectStatus.Completed => "bg-secondary",
            ProjectStatus.Draft => "bg-info",
            _ => "bg-secondary"
        };
    }

    #endregion

    #region Unit Details

    public class UnitDetailsViewModel
    {
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        
        // Unit Info
        public UnitType UnitType { get; set; }
        public int Bedrooms { get; set; }
        public int Bathrooms { get; set; }
        public decimal SquareFootage { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal? CurrentAppraisalValue { get; set; }
        public DateTime? OccupancyDate { get; set; }
        public DateTime? ClosingDate { get; set; }
        
        // Purchaser Info
        public PurchaserInfoViewModel? PrimaryPurchaser { get; set; }
        public List<PurchaserInfoViewModel> AllPurchasers { get; set; } = new();
        
        // SOA
        public SOAViewModel? SOA { get; set; }
        
        // Shortfall
        public ShortfallViewModel? Shortfall { get; set; }
        
        // Deposits
        public List<DepositViewModel> Deposits { get; set; } = new();
        
        // Documents
        public List<DocumentViewModel> Documents { get; set; } = new();
        
        // Status
        public UnitStatus Status { get; set; }
        public ClosingRecommendation? Recommendation { get; set; }
        public bool IsConfirmedByLawyer { get; set; }

        // NEW: APS (Agreement of Purchase and Sale) date
        public DateTime? APSDate { get; set; }

        // NEW: Interim occupancy start date (for occupancy calculations)
        public DateTime? InterimOccupancyStartDate { get; set; }

        // NEW: Is first-time buyer (for LTT rebate calculations)
        public bool IsFirstTimeBuyer { get; set; } = false;

        public List<LawyerAssignmentViewModel> LawyerAssignments { get; set; } = new();
        public int TotalUnreadLawyerNotes { get; set; }
        public bool HasLawyerActivity => LawyerAssignments.Any();
    }

    public class PurchaserInfoViewModel
    {
        public string PurchaserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public bool IsPrimary { get; set; }
        public decimal OwnershipPercentage { get; set; }
        
        // Mortgage
        public bool HasMortgageApproval { get; set; }
        public string? MortgageProvider { get; set; }
        public decimal? MortgageAmount { get; set; }
        public MortgageApprovalType ApprovalType { get; set; }
        public DateTime? ApprovalExpiryDate { get; set; }
        
        // Financials (limited visibility)
        public decimal? AdditionalCashAvailable { get; set; }
    }

    public class SOAViewModel
    {
        // Debits
        public decimal PurchasePrice { get; set; }
        public decimal LandTransferTax { get; set; }
        public decimal TorontoLandTransferTax { get; set; }
        public decimal DevelopmentCharges { get; set; }
        public decimal TarionFee { get; set; }
        public decimal UtilityConnectionFees { get; set; }
        public decimal PropertyTaxAdjustment { get; set; }
        public decimal CommonExpenseAdjustment { get; set; }
        public decimal OccupancyFeesOwing { get; set; }
        public decimal ParkingPrice { get; set; }
        public decimal LockerPrice { get; set; }
        public decimal Upgrades { get; set; }
        public decimal LegalFeesEstimate { get; set; }
        public decimal OtherDebits { get; set; }
        public decimal TotalDebits { get; set; }
        
        // Credits
        public decimal DepositsPaid { get; set; }
        public decimal DepositInterest { get; set; }
        public decimal BuilderCredits { get; set; }
        public decimal OtherCredits { get; set; }
        public decimal TotalCredits { get; set; }
        
        // Final
        public decimal BalanceDueOnClosing { get; set; }
        public decimal MortgageAmount { get; set; }
        public decimal CashRequiredToClose { get; set; }
        
        // Meta
        public DateTime CalculatedAt { get; set; }
        public bool IsConfirmedByLawyer { get; set; }
        public string? LawyerNotes { get; set; }

        // Lawyer SOA comparison
        public decimal? LawyerUploadedBalanceDue { get; set; }
        public bool HasSoaMismatch => LawyerUploadedBalanceDue.HasValue
            && Math.Abs(BalanceDueOnClosing - LawyerUploadedBalanceDue.Value) >= 1m;
    }

    public class ShortfallViewModel
    {
        public decimal SOAAmount { get; set; }
        public decimal MortgageApproved { get; set; }
        public decimal DepositsPaid { get; set; }
        public decimal AdditionalCashAvailable { get; set; }
        public decimal TotalFundsAvailable { get; set; }
        public decimal ShortfallAmount { get; set; }
        public decimal ShortfallPercentage { get; set; }
        public RiskLevel RiskLevel { get; set; }
        public ClosingRecommendation Recommendation { get; set; }
        public decimal? SuggestedDiscount { get; set; }
        public decimal? SuggestedVTBAmount { get; set; }
        public string? RecommendationReasoning { get; set; }
        public DateTime CalculatedAt { get; set; }
    }

    public class DepositViewModel
    {
        public int Id { get; set; }
        public string DepositName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? PaidDate { get; set; }
        public bool IsPaid { get; set; }
        public DepositStatus Status { get; set; }
        // NEW: Deposit holder information
        [StringLength(50)]
        public string Holder { get; set; } = "Builder"; // Builder, Trust, Lawyer

        // NEW: Interest eligibility (some APS agreements require interest paid to buyer)
        public bool IsInterestEligible { get; set; } = false;

        // NEW: Interest rate (APS-defined, annual rate)
        [Column(TypeName = "decimal(5,3)")]
        public decimal? InterestRate { get; set; }

        // NEW: Compounding type
        public InterestCompoundingType CompoundingType { get; set; } = InterestCompoundingType.Simple;

        // Priority 6 — per-period government rate schedule for daily simple interest
        public List<DepositInterestPeriodViewModel> InterestPeriods { get; set; } = new();
    }

    public class DepositInterestPeriodViewModel
    {
        public int Id { get; set; }
        public int DepositId { get; set; }
        public string DepositName { get; set; } = string.Empty;
        public int UnitId { get; set; }
        [Required] public DateTime PeriodStart { get; set; }
        [Required] public DateTime PeriodEnd { get; set; }
        /// <summary>Annual rate as a percentage, e.g. 1.500 = 1.5%</summary>
        [Required][Range(0.001, 99.999)] public decimal AnnualRate { get; set; }
    }

    // InterestCompoundingType and DepositHolder are defined in Models/Entities/AllEntities.cs

    #endregion

    #region Forms - Builder Input

    public class CreateProjectViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = "Toronto";
        public string PostalCode { get; set; } = string.Empty;
        public ProjectType ProjectType { get; set; }
        public int TotalUnits { get; set; }
        public DateTime? OccupancyDate { get; set; }
        public DateTime? ClosingDate { get; set; }
    }

    public class CreateUnitViewModel
    {
        public int ProjectId { get; set; }
        public string UnitNumber { get; set; } = string.Empty;
        public string? FloorNumber { get; set; }
        public UnitType UnitType { get; set; }
        public int Bedrooms { get; set; }
        public int Bathrooms { get; set; }
        public decimal SquareFootage { get; set; }
        public decimal PurchasePrice { get; set; }
        public bool HasParking { get; set; }
        public decimal ParkingPrice { get; set; }
        public bool HasLocker { get; set; }
        public decimal LockerPrice { get; set; }
        public DateTime? OccupancyDate { get; set; }
        public DateTime? ClosingDate { get; set; }
        public decimal? ActualAnnualLandTax { get; set; }
        public decimal? ActualMonthlyMaintenanceFee { get; set; }
    }

    #endregion

    #region Forms - Purchaser Input

    public class PurchaserSubmissionViewModel
    {
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        
        // Personal Info (pre-filled from account)
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        
        // Mortgage Info
        public bool HasMortgageApproval { get; set; }
        public MortgageApprovalType ApprovalType { get; set; }
        public string? MortgageProvider { get; set; }
        public decimal? ApprovedAmount { get; set; }
        public decimal? InterestRate { get; set; }
        public DateTime? ApprovalExpiryDate { get; set; }
        
        // Financial Info
        public decimal? AnnualIncome { get; set; }
        public decimal? AdditionalCashAvailable { get; set; }
        public EmploymentStatus EmploymentStatus { get; set; }
        public string? Employer { get; set; }
        
        // Documents to upload
        // Handled via file upload
    }

    #endregion

    #region Lawyer Review

    public class LawyerReviewViewModel
    {
        public int AssignmentId { get; set; }
        public int UnitId { get; set; }

        // Project Info
        public string ProjectName { get; set; } = "";
        public string ProjectAddress { get; set; } = "";
        public string? BuilderName { get; set; }

        // Unit Info
        public string UnitNumber { get; set; } = "";
        public UnitType UnitType { get; set; }
        public int Bedrooms { get; set; }
        public int Bathrooms { get; set; }
        public int SquareFootage { get; set; }
        public decimal PurchasePrice { get; set; }
        public DateTime? OccupancyDate { get; set; }
        public DateTime? ClosingDate { get; set; }

        // Purchaser Info
        public string PurchaserName { get; set; } = "";
        public string? PurchaserEmail { get; set; }
        public string? PurchaserPhone { get; set; }
        public List<string> CoPurchasers { get; set; } = new();

        // Mortgage Info
        public bool HasMortgageApproval { get; set; }
        public string? MortgageProvider { get; set; }
        public decimal MortgageAmount { get; set; }
        public MortgageApprovalType MortgageApprovalType { get; set; }
        public DateTime? MortgageExpiryDate { get; set; }
        public string? MortgageConditions { get; set; }

        // Financial Info
        public decimal AdditionalCashAvailable { get; set; }
        public decimal TotalFundsAvailable { get; set; }

        // Deposits
        public List<DepositViewModel> Deposits { get; set; } = new();
        public decimal TotalDeposits { get; set; }
        public decimal DepositsPaid { get; set; }

        // SOA
        public bool HasSOA { get; set; }
        public StatementOfAdjustments? SOA { get; set; }

        // Shortfall
        public decimal ShortfallAmount { get; set; }
        public decimal ShortfallPercentage { get; set; }
        public ClosingRecommendation? Recommendation { get; set; }
        public string? RecommendationReasoning { get; set; }

        // Review Status
        public LawyerReviewStatus ReviewStatus { get; set; }
        public DateTime AssignedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }

        // Notes
        public List<LawyerNoteViewModel> Notes { get; set; } = new();
        public decimal TotalDepositsPaid { get; set; }
    }

    // ===== LAWYER DASHBOARD VIEW MODEL =====
    public class LawyerDashboardViewModel
    {
        public string LawyerName { get; set; } = "";
        public string? LawyerFirm { get; set; }

        // Summary Stats
        public int TotalAssigned { get; set; }
        public int PendingReview { get; set; }
        public int UnderReview { get; set; }
        public int Approved { get; set; }
        public int NeedsAttention { get; set; }

        public List<LawyerUnitViewModel> Units { get; set; } = new();
    }

    public class LawyerProjectViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public int TotalUnits { get; set; }
        public int UnitsAwaitingReview { get; set; }
        public int UnitsConfirmed { get; set; }
    }

    #endregion

    #region API DTOs

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }
    }

    public class CalculationResultDto
    {
        public int UnitId { get; set; }
        public decimal SOAAmount { get; set; }
        public decimal ShortfallAmount { get; set; }
        public decimal ShortfallPercentage { get; set; }
        public string Recommendation { get; set; } = string.Empty;
        public string? Reasoning { get; set; }
    }

    public class ProjectStatsDto
    {
        public int ProjectId { get; set; }
        public int TotalUnits { get; set; }
        public Dictionary<string, int> UnitsByStatus { get; set; } = new();
        public decimal TotalDiscountsNeeded { get; set; }
        public decimal TotalAtRisk { get; set; }
        public decimal ClosingProbability { get; set; }
    }

    #endregion

    #region Purchaser
    public class AcceptInvitationViewModel
    {
        public string Email { get; set; } = "";
        public string Code { get; set; } = "";
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? UnitNumber { get; set; }
        public string? ProjectName { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Required(ErrorMessage = "Please confirm your password")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = "";
    }

    // ===== PURCHASER DASHBOARD =====
    public class PurchaserDashboardViewModel
    {
        public string PurchaserName { get; set; } = "";
        public List<PurchaserUnitViewModel> Units { get; set; } = new();
        public bool HasDocumentsUploaded { get; set; }
        public int DocumentsUploadedCount { get; set; }
        public int RequiredDocumentsCount { get; set; } = 3; 
        public List<DocumentViewModel> UploadedDocuments { get; set; } = new List<DocumentViewModel>();
    }

    public class PurchaserUnitViewModel
    {
        public int UnitPurchaserId { get; set; }
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string ProjectAddress { get; set; } = "";
        public decimal PurchasePrice { get; set; }
        public DateTime? ClosingDate { get; set; }
        public UnitStatus Status { get; set; }

        // Mortgage
        public bool HasMortgageInfo { get; set; }
        public bool MortgageApproved { get; set; }
        public decimal MortgageAmount { get; set; }
        public string? MortgageProvider { get; set; }

        // Financials
        public bool HasFinancialsSubmitted { get; set; }
        public decimal AdditionalCashAvailable { get; set; }

        // Deposits
        public decimal TotalDeposits { get; set; }
        public decimal DepositsPaid { get; set; }

        // Document tracking
        public bool HasDocumentsUploaded { get; set; }
        public int DocumentsUploadedCount { get; set; }
        public int RequiredDocumentsCount { get; set; }

        // SOA
        public bool HasSOA { get; set; }
        public decimal BalanceDueOnClosing { get; set; }
        public decimal CashRequiredToClose { get; set; }

        // Shortfall
        public decimal ShortfallAmount { get; set; }
        public decimal ShortfallPercentage { get; set; }
        public ClosingRecommendation? Recommendation { get; set; }

        // Completion tracking
        public List<CompletionStep> CompletionSteps { get; set; } = new();
        public int CompletionPercentage { get; set; }

        // Helper properties
        public int DaysUntilClosing => ClosingDate.HasValue
            ? (int)(ClosingDate.Value - DateTime.Now).TotalDays
            : 0;

        public string StatusDisplay => Status.ToString();
        public string StatusClass => Status switch
        {
            UnitStatus.ReadyToClose => "success",
            UnitStatus.NeedsDiscount => "primary",
            UnitStatus.NeedsVTB => "warning",
            UnitStatus.AtRisk => "danger",
            UnitStatus.Closed => "secondary",
            _ => "info"
        };
    }

    public class CompletionStep
    {
        public string Name { get; set; } = "";
        public bool IsComplete { get; set; }
    }

    // ===== SUBMIT MORTGAGE INFO =====
    public class SubmitMortgageInfoViewModel
    {
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public decimal PurchasePrice { get; set; }

        [Display(Name = "Do you have a mortgage approval?")]
        public bool HasMortgageApproval { get; set; }

        [Display(Name = "Approval Type")]
        public MortgageApprovalType ApprovalType { get; set; }

        [Display(Name = "Lender/Mortgage Provider")]
        public string? MortgageProvider { get; set; }

        [Display(Name = "Approved Mortgage Amount")]
        [DataType(DataType.Currency)]
        public decimal? ApprovedAmount { get; set; }

        [Display(Name = "Interest Rate (%)")]
        public decimal? InterestRate { get; set; }

        [Display(Name = "Amortization (Years)")]
        public int? AmortizationYears { get; set; } = 25;

        [Display(Name = "Approval Expiry Date")]
        [DataType(DataType.Date)]
        public DateTime? ApprovalExpiryDate { get; set; }

        [Display(Name = "Does your approval have conditions?")]
        public bool HasConditions { get; set; }

        [Display(Name = "Conditions (if any)")]
        public string? Conditions { get; set; }

        [Display(Name = "Is this a blanket mortgage?")]
        public bool IsBlanketMortgage { get; set; }

        [Display(Name = "Appraised Value (required if no blanket mortgage)")]
        [DataType(DataType.Currency)]
        public decimal? PurchaserAppraisalValue { get; set; }

        [Display(Name = "Estimated Mortgage Funding / Closing Date")]
        [DataType(DataType.Date)]
        public DateTime? EstimatedFundingDate { get; set; }

        [Display(Name = "Credit Score")]
        public int? CreditScore { get; set; }

        [Display(Name = "Credit Bureau (TransUnion / Equifax)")]
        public string? CreditBureau { get; set; }

        [Display(Name = "Comments / Notes")]
        public string? Comments { get; set; }

        // Calculated helper
        public decimal SuggestedMortgageAmount => PurchasePrice * 0.80m; // 80% LTV
    }

    // ===== SUBMIT FINANCIALS =====
    public class SubmitFinancialsViewModel
    {
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = "";
        public string ProjectName { get; set; } = "";

        [Display(Name = "Cash/Savings Available")]
        [DataType(DataType.Currency)]
        public decimal? AdditionalCashAvailable { get; set; }

        [Display(Name = "RRSP (First-Time Home Buyer Plan)")]
        [DataType(DataType.Currency)]
        public decimal? RRSPAvailable { get; set; }

        [Display(Name = "Gift from Family")]
        [DataType(DataType.Currency)]
        public decimal? GiftFromFamily { get; set; }

        [Display(Name = "Proceeds from Property Sale")]
        [DataType(DataType.Currency)]
        public decimal? ProceedsFromSale { get; set; }

        [Display(Name = "Other Funds Description")]
        public string? OtherFundsDescription { get; set; }

        [Display(Name = "Other Funds Amount")]
        [DataType(DataType.Currency)]
        public decimal? OtherFundsAmount { get; set; }

        [Display(Name = "Do you have a property to sell?")]
        public bool HasExistingPropertyToSell { get; set; }

        [Display(Name = "Current Property Estimated Value")]
        [DataType(DataType.Currency)]
        public decimal? ExistingPropertyValue { get; set; }

        [Display(Name = "Existing Mortgage Balance")]
        [DataType(DataType.Currency)]
        public decimal? ExistingMortgageBalance { get; set; }

        [Display(Name = "Is the property currently listed?")]
        public bool IsPropertyListed { get; set; }

        [Display(Name = "Expected Sale Date")]
        [DataType(DataType.Date)]
        public DateTime? ExpectedSaleDate { get; set; }

        // Calculated total
        public decimal TotalFundsAvailable =>
            (AdditionalCashAvailable ?? 0) +
            (RRSPAvailable ?? 0) +
            (GiftFromFamily ?? 0) +
            (ProceedsFromSale ?? 0) +
            (OtherFundsAmount ?? 0);
    }

    // ===== SUBMIT EXTENSION REQUEST (Purchaser) =====
    public class SubmitExtensionRequestViewModel
    {
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public DateTime? CurrentClosingDate { get; set; }
        public int? ExistingRequestId { get; set; }

        [Required]
        [Display(Name = "Requested New Closing Date")]
        [DataType(DataType.Date)]
        public DateTime RequestedNewClosingDate { get; set; }

        [Required]
        [Display(Name = "Reason for Extension / Reschedule")]
        public string Reason { get; set; } = "";
    }

    // ===== REVIEW EXTENSION REQUEST (Builder) =====
    public class ReviewExtensionRequestViewModel
    {
        public int RequestId { get; set; }
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string PurchaserName { get; set; } = "";
        public DateTime OriginalClosingDate { get; set; }
        public DateTime RequestedNewClosingDate { get; set; }
        public string Reason { get; set; } = "";
        public DateTime RequestedDate { get; set; }

        [Display(Name = "Reviewer Notes")]
        public string? ReviewerNotes { get; set; }

        public bool Approve { get; set; }
    }

    // ===== REVIEW AI SUGGESTION (Builder) =====
    public class ReviewSuggestionViewModel
    {
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = "";

        [Required]
        [Display(Name = "Decision")]
        public string Decision { get; set; } = "";  // "Accept", "Modify", "Reject"

        [Display(Name = "Modified Suggestion Notes")]
        public string? ModifiedSuggestion { get; set; }
    }

    #endregion

    #region Bulk Import

    /// <summary>
    /// ViewModel for the Bulk Import page
    /// </summary>
    public class BulkImportViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public int? MaxUnits { get; set; }
        public int CurrentUnitCount { get; set; }
        public int? RemainingSlots => MaxUnits.HasValue ? MaxUnits.Value - CurrentUnitCount : null;
    }

    /// <summary>
    /// CSV import row structure for bulk unit import
    /// Maps to CSV columns for CsvHelper
    /// </summary>
    // <summary>
    /// CSV import row structure for bulk unit import
    /// Updated for SOA Compliance - includes HST rebate and deposit interest fields
    /// </summary>
    public class BulkImportRow
    {
        // ===== UNIT INFORMATION (Required) =====
        public string? UnitNumber { get; set; }
        public string? FloorNumber { get; set; }
        public string? UnitType { get; set; }
        public int Bedrooms { get; set; }
        public int Bathrooms { get; set; }
        public decimal SquareFootage { get; set; }
        public decimal PurchasePrice { get; set; }

        // ===== PARKING & LOCKER (Optional) =====
        public string? HasParking { get; set; }
        public decimal ParkingPrice { get; set; }
        public string? HasLocker { get; set; }
        public decimal LockerPrice { get; set; }

        // ===== DATES (Optional - defaults to project dates) =====
        public DateTime? OccupancyDate { get; set; }
        public DateTime? ClosingDate { get; set; }

        // ===== SOA ENHANCEMENT FIELDS =====
        /// <summary>
        /// Agreement of Purchase and Sale date
        /// </summary>
        public DateTime? APSDate { get; set; }

        /// <summary>
        /// Is purchaser a first-time home buyer (affects LTT rebate)
        /// Values: true/false, yes/no, 1/0
        /// </summary>
        public string? IsFirstTimeBuyer { get; set; }

        /// <summary>
        /// Will unit be purchaser's primary residence (affects HST rebate)
        /// Values: true/false, yes/no, 1/0
        /// </summary>
        public string? IsPrimaryResidence { get; set; }

        // ===== SOA ADJUSTMENT FIELDS (Priority 6C) =====
        /// <summary>
        /// Actual annual property tax from builder's tax bill. Null = use 1% estimate.
        /// </summary>
        public decimal? ActualAnnualLandTax { get; set; }

        /// <summary>
        /// Actual monthly maintenance/common expense fee. Null = use $0.60/sqft estimate.
        /// </summary>
        public decimal? ActualMonthlyMaintenanceFee { get; set; }

        // ===== PURCHASER INFORMATION (Optional) =====
        public string? PurchaserEmail { get; set; }
        public string? PurchaserFirstName { get; set; }
        public string? PurchaserLastName { get; set; }
        public string? PurchaserPhone { get; set; }

        // ===== DEPOSITS (Optional - up to 5 deposits) =====
        // Deposit 1
        public decimal Deposit1Amount { get; set; }
        public DateTime? Deposit1DueDate { get; set; }
        public DateTime? Deposit1PaidDate { get; set; }
        public string? Deposit1Holder { get; set; }           // Builder, Trust, Lawyer
        public string? Deposit1InterestEligible { get; set; } // true/false
        public decimal? Deposit1InterestRate { get; set; }    // e.g., 0.02 for 2%

        // Deposit 2
        public decimal Deposit2Amount { get; set; }
        public DateTime? Deposit2DueDate { get; set; }
        public DateTime? Deposit2PaidDate { get; set; }
        public string? Deposit2Holder { get; set; }
        public string? Deposit2InterestEligible { get; set; }
        public decimal? Deposit2InterestRate { get; set; }

        // Deposit 3
        public decimal Deposit3Amount { get; set; }
        public DateTime? Deposit3DueDate { get; set; }
        public DateTime? Deposit3PaidDate { get; set; }
        public string? Deposit3Holder { get; set; }
        public string? Deposit3InterestEligible { get; set; }
        public decimal? Deposit3InterestRate { get; set; }

        // Deposit 4
        public decimal Deposit4Amount { get; set; }
        public DateTime? Deposit4DueDate { get; set; }
        public DateTime? Deposit4PaidDate { get; set; }
        public string? Deposit4Holder { get; set; }
        public string? Deposit4InterestEligible { get; set; }
        public decimal? Deposit4InterestRate { get; set; }

        // Deposit 5
        public decimal Deposit5Amount { get; set; }
        public DateTime? Deposit5DueDate { get; set; }
        public DateTime? Deposit5PaidDate { get; set; }
        public string? Deposit5Holder { get; set; }
        public string? Deposit5InterestEligible { get; set; }
        public decimal? Deposit5InterestRate { get; set; }
    }
    #endregion

    public class ProjectPurchasersViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public int TotalUnits { get; set; }
        public List<PurchaserListItemViewModel> Purchasers { get; set; } = new();
    }

    public class PurchaserListItemViewModel
    {
        public int UnitPurchaserId { get; set; }
        public string UserId { get; set; } = "";

        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public bool IsPrimary { get; set; }

        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = "";
        public decimal PurchasePrice { get; set; }

        public bool HasActivated { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime AddedAt { get; set; }

        public bool HasSubmittedMortgageInfo { get; set; }
        public bool MortgageApproved { get; set; }
        public decimal MortgageAmount { get; set; }

        public bool HasSubmittedFinancials { get; set; }
        public decimal TotalFundsAvailable { get; set; }
    }

    #region LAWYERS
    public class LawyerUnitViewModel
    {
        public int AssignmentId { get; set; }
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string ProjectAddress { get; set; } = "";
        public decimal PurchasePrice { get; set; }
        public DateTime? ClosingDate { get; set; }

        // Purchaser
        public string PurchaserName { get; set; } = "";
        public string? PurchaserEmail { get; set; }

        // Status
        public UnitStatus UnitStatus { get; set; }
        public LawyerReviewStatus ReviewStatus { get; set; }
        public DateTime AssignedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }

        // SOA
        public bool HasSOA { get; set; }
        public decimal BalanceDueOnClosing { get; set; }
        public decimal CashRequiredToClose { get; set; }
        public decimal? LawyerUploadedBalanceDue { get; set; }
        public bool HasSoaMismatch => LawyerUploadedBalanceDue.HasValue
            && Math.Abs(BalanceDueOnClosing - LawyerUploadedBalanceDue.Value) >= 1m;

        // Shortfall
        public decimal ShortfallAmount { get; set; }
        public ClosingRecommendation? Recommendation { get; set; }

        // Mortgage
        public bool HasMortgageApproval { get; set; }
        public decimal MortgageAmount { get; set; }

        // Deposits
        public decimal TotalDeposits { get; set; }
        public decimal DepositsPaid { get; set; }

        public int DaysUntilClosing { get; set; }

        // Helper properties
        public string ReviewStatusClass => ReviewStatus switch
        {
            LawyerReviewStatus.Pending => "warning",
            LawyerReviewStatus.UnderReview => "info",
            LawyerReviewStatus.Approved => "success",
            LawyerReviewStatus.NeedsRevision => "danger",
            _ => "secondary"
        };
    }

    // ===== LAWYER ACCEPT INVITATION VIEW MODEL =====
    public class LawyerAcceptInvitationViewModel
    {
        public string Email { get; set; } = "";
        public string Code { get; set; } = "";
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? FirmName { get; set; }
        public int AssignedUnitsCount { get; set; }
        public List<string> ProjectNames { get; set; } = new();

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Required(ErrorMessage = "Please confirm your password")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = "";
    }

    // ===== PROJECT LAWYERS VIEW MODEL =====
    public class ProjectLawyersViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public int TotalUnits { get; set; }
        public List<LawyerListItemViewModel> Lawyers { get; set; } = new();
    }

    public class LawyerListItemViewModel
    {
        public string LawyerId { get; set; } = "";

        // Lawyer Info
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? LawFirm { get; set; }

        // Account Status
        public bool HasActivated { get; set; }
        public DateTime? LastLoginAt { get; set; }

        // Assignment Stats
        public int AssignedUnitsCount { get; set; }
        public List<string> AssignedUnitNumbers { get; set; } = new();
        public int PendingCount { get; set; }
        public int UnderReviewCount { get; set; }
        public int ApprovedCount { get; set; }
        public int NeedsRevisionCount { get; set; }

        // Detailed Assignments
        public List<LawyerAssignmentDetailViewModel> Assignments { get; set; } = new();
    }

    public class LawyerAssignmentDetailViewModel
    {
        public int AssignmentId { get; set; }
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = "";
        public LawyerReviewStatus ReviewStatus { get; set; }
        public DateTime AssignedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
    }

    public class BulkAssignLawyerViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = "";

        // Existing lawyers in the system (not just this project)
        public List<ApplicationUser> ExistingLawyers { get; set; } = new();

        // All units in the project
        public List<BulkAssignUnitViewModel> Units { get; set; } = new();
    }

    public class BulkAssignUnitViewModel
    {
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = "";
        public string? UnitType { get; set; }
        public decimal PurchasePrice { get; set; }
        public string? PurchaserName { get; set; }
        public bool HasLawyer { get; set; }
        public bool LawyerConfirmed { get; set; }
        public List<string> AssignedLawyers { get; set; } = new();
    }
    #endregion

    #region APS Document Analysis

    /// <summary>
    /// ViewModel for the Upload APS page
    /// </summary>
    public class UploadApsViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
    }

    /// <summary>
    /// ViewModel for reviewing AI-extracted APS data
    /// </summary>
    public class ReviewApsDataViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public string FileName { get; set; } = "";
        public ApsExtractedData? ExtractedData { get; set; }

        // Unit Information (editable)
        public string? UnitNumber { get; set; }
        public string? FloorNumber { get; set; }
        public string? UnitType { get; set; }
        public int? Bedrooms { get; set; }
        public int? Bathrooms { get; set; }
        public decimal? SquareFootage { get; set; }
        public decimal? PurchasePrice { get; set; }

        // Parking & Locker
        public bool HasParking { get; set; }
        public decimal? ParkingPrice { get; set; }
        public bool HasLocker { get; set; }
        public decimal? LockerPrice { get; set; }

        // Dates
        public DateTime? OccupancyDate { get; set; }
        public DateTime? ClosingDate { get; set; }

        // Primary Purchaser
        public string? PurchaserFirstName { get; set; }
        public string? PurchaserLastName { get; set; }
        public string? PurchaserEmail { get; set; }
        public string? PurchaserPhone { get; set; }

        // Co-Purchaser (optional)
        public string? CoPurchaserFirstName { get; set; }
        public string? CoPurchaserLastName { get; set; }
        public string? CoPurchaserEmail { get; set; }
        public string? CoPurchaserPhone { get; set; }

        // Deposits
        public decimal Deposit1Amount { get; set; }
        public DateTime? Deposit1DueDate { get; set; }
        public bool Deposit1Paid { get; set; }

        public decimal Deposit2Amount { get; set; }
        public DateTime? Deposit2DueDate { get; set; }
        public bool Deposit2Paid { get; set; }

        public decimal Deposit3Amount { get; set; }
        public DateTime? Deposit3DueDate { get; set; }
        public bool Deposit3Paid { get; set; }

        public decimal Deposit4Amount { get; set; }
        public DateTime? Deposit4DueDate { get; set; }
        public bool Deposit4Paid { get; set; }

        public decimal Deposit5Amount { get; set; }
        public DateTime? Deposit5DueDate { get; set; }
        public bool Deposit5Paid { get; set; }
    }

    #endregion

    #region Lawyer Visibility ViewModels

    /// <summary>
    /// Lawyer assignment info for builder view
    /// </summary>
    public class LawyerAssignmentViewModel
    {
        public int AssignmentId { get; set; }
        public string LawyerId { get; set; } = "";
        public string LawyerName { get; set; } = "";
        public string LawyerEmail { get; set; } = "";
        public string? LawyerPhone { get; set; }
        public string? LawFirm { get; set; }
        public LawyerRole Role { get; set; }
        public string RoleDisplay => Role switch
        {
            LawyerRole.BuilderLawyer => "Builder's Lawyer",
            LawyerRole.PurchaserLawyer => "Purchaser's Lawyer",
            LawyerRole.ReviewingLawyer => "Reviewing Lawyer",
            _ => "Lawyer"
        };
        public LawyerReviewStatus ReviewStatus { get; set; }
        public string StatusBadgeClass => ReviewStatus switch
        {
            LawyerReviewStatus.Pending => "bg-secondary",
            LawyerReviewStatus.UnderReview => "bg-info",
            LawyerReviewStatus.Approved => "bg-success",
            LawyerReviewStatus.NeedsRevision => "bg-warning",
            _ => "bg-secondary"
        };
        public DateTime AssignedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public bool IsActive { get; set; }

        /// <summary>
        /// Notes visible to builder
        /// </summary>
        public List<LawyerNoteViewModel> BuilderNotes { get; set; } = new();

        /// <summary>
        /// Count of unread notes for builder
        /// </summary>
        public int UnreadNotesCount { get; set; }
    }

    /// <summary>
    /// Lawyer note for builder view
    /// </summary>
    public class LawyerNoteViewModel
    {
        public int NoteId { get; set; }
        public string Note { get; set; } = "";
        public LawyerNoteType NoteType { get; set; }
        public int Visibility { get; set; } = 1;
        public bool IsReadByBuilder { get; set; } = false;
        public string LawyerName { get; set; } = "";
        public string NoteTypeDisplay => NoteType switch
        {
            LawyerNoteType.General => "Note",
            LawyerNoteType.Question => "Question",
            LawyerNoteType.Concern => "Concern",
            LawyerNoteType.RevisionRequest => "Revision Request",
            LawyerNoteType.Approval => "Approval Note",
            _ => "Note"
        };
        public string NoteTypeIcon => NoteType switch
        {
            LawyerNoteType.Question => "bi-question-circle",
            LawyerNoteType.Concern => "bi-exclamation-triangle",
            LawyerNoteType.RevisionRequest => "bi-arrow-repeat",
            LawyerNoteType.Approval => "bi-check-circle",
            _ => "bi-chat-left-text"
        };
        public string NoteTypeColor => NoteType switch
        {
            LawyerNoteType.Question => "text-info",
            LawyerNoteType.Concern => "text-warning",
            LawyerNoteType.RevisionRequest => "text-danger",
            LawyerNoteType.Approval => "text-success",
            _ => "text-secondary"
        };
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
    }

    /// <summary>
    /// Summary of lawyer activity for dashboard cards
    /// </summary>
    public class LawyerActivitySummary
    {
        public int TotalAssigned { get; set; }
        public int PendingReview { get; set; }
        public int UnderReview { get; set; }
        public int Approved { get; set; }
        public int NeedsRevision { get; set; }
        public int UnreadNotes { get; set; }
    }

    #endregion

    #region Admin ViewModels

    /// <summary>
    /// Admin dashboard overview
    /// </summary>
    public class AdminDashboardViewModel
    {
        public int TotalBuilders { get; set; }
        public int TotalPurchasers { get; set; }
        public int TotalLawyers { get; set; }
        public int TotalAdmins { get; set; }
        public int TotalUsers => TotalBuilders + TotalPurchasers + TotalLawyers + TotalAdmins;

        public int TotalProjects { get; set; }
        public int TotalUnits { get; set; }

        public List<UserListItemViewModel> RecentUsers { get; set; } = new();
        public List<UserListItemViewModel> RecentlyActiveUsers { get; set; } = new();
    }

    /// <summary>
    /// User list with filtering and pagination
    /// </summary>
    public class UserListViewModel
    {
        public List<UserListItemViewModel> Users { get; set; } = new();
        public string? Search { get; set; }
        public string? UserTypeFilter { get; set; }
        public string? StatusFilter { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
    }

    /// <summary>
    /// Single user in a list
    /// </summary>
    public class UserListItemViewModel
    {
        public string UserId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Phone { get; set; }
        public UserType UserType { get; set; }
        public bool IsActive { get; set; }
        public bool EmailConfirmed { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsSuperAdmin { get; set; }

        public string UserTypeBadgeClass => UserType switch
        {
            UserType.PlatformAdmin => "bg-danger",
            UserType.Builder => "bg-primary",
            UserType.Purchaser => "bg-success",
            UserType.Lawyer => "bg-info",
            _ => "bg-secondary"
        };

        public string StatusBadgeClass => IsActive ? "bg-success" : "bg-secondary";

        public string LastLoginDisplay => LastLoginAt.HasValue
            ? GetTimeAgo(LastLoginAt.Value)
            : "Never";

        private static string GetTimeAgo(DateTime dateTime)
        {
            var span = DateTime.UtcNow - dateTime;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} hrs ago";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays} days ago";
            if (span.TotalDays < 30) return $"{(int)(span.TotalDays / 7)} weeks ago";
            return dateTime.ToString("MMM dd, yyyy");
        }
    }

    /// <summary>
    /// Detailed user view for admin
    /// </summary>
    public class AdminUserDetailViewModel
    {
        public string UserId { get; set; } = "";
        public string Email { get; set; } = "";
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string FullName => $"{FirstName} {LastName}".Trim();
        public string? Phone { get; set; }
        public string? CompanyName { get; set; }
        public UserType UserType { get; set; }
        public List<string> Roles { get; set; } = new();
        public bool IsActive { get; set; }
        public bool EmailConfirmed { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool IsSuperAdmin { get; set; }
        public bool IsCurrentUserSuperAdmin { get; set; }
        public int MaxProjects { get; set; }

        // Builder-specific data
        public List<BuilderProjectSummary> BuilderProjects { get; set; } = new();
        public int TotalProjects { get; set; }
        public int TotalUnits { get; set; }

        // Purchaser-specific data
        public List<PurchaserUnitSummary> PurchaserUnits { get; set; } = new();

        // Lawyer-specific data
        public List<LawyerAssignmentSummary> LawyerAssignments { get; set; } = new();

        // Activity log
        public List<UserActivityItem> RecentActivity { get; set; } = new();

        public string UserTypeBadgeClass => UserType switch
        {
            UserType.PlatformAdmin => "bg-danger",
            UserType.Builder => "bg-primary",
            UserType.Purchaser => "bg-success",
            UserType.Lawyer => "bg-info",
            _ => "bg-secondary"
        };
    }

    /// <summary>
    /// Builder's project summary for admin view
    /// </summary>
    public class BuilderProjectSummary
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public string? Address { get; set; }
        public int TotalUnits { get; set; }
        public int? MaxUnits { get; set; }
        public int PendingUnits { get; set; }
        public int AtRiskUnits { get; set; }
        public int ClosedUnits { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Purchaser's unit summary for admin view
    /// </summary>
    public class PurchaserUnitSummary
    {
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public decimal PurchasePrice { get; set; }
        public DateTime? ClosingDate { get; set; }
        public UnitStatus Status { get; set; }
        public bool IsPrimary { get; set; }
        public bool HasMortgageApproval { get; set; }
        public decimal MortgageAmount { get; set; }
    }

    /// <summary>
    /// Lawyer's assignment summary for admin view
    /// </summary>
    public class LawyerAssignmentSummary
    {
        public int AssignmentId { get; set; }
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public LawyerRole Role { get; set; }
        public LawyerReviewStatus ReviewStatus { get; set; }
        public DateTime AssignedAt { get; set; }

        public string StatusBadgeClass => ReviewStatus switch
        {
            LawyerReviewStatus.Pending => "bg-secondary",
            LawyerReviewStatus.UnderReview => "bg-info",
            LawyerReviewStatus.Approved => "bg-success",
            LawyerReviewStatus.NeedsRevision => "bg-warning",
            _ => "bg-secondary"
        };
    }

    /// <summary>
    /// User activity log item
    /// </summary>
    public class UserActivityItem
    {
        public string Action { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string Icon { get; set; } = "bi-activity";

        public string TimeAgo
        {
            get
            {
                var span = DateTime.UtcNow - Timestamp;
                if (span.TotalMinutes < 1) return "Just now";
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours} hrs ago";
                if (span.TotalDays < 7) return $"{(int)span.TotalDays} days ago";
                return Timestamp.ToString("MMM dd, yyyy");
            }
        }
    }

    /// <summary>
    /// Admin edit user form
    /// </summary>
    public class AdminEditUserViewModel
    {
        public string UserId { get; set; } = "";

        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = "";

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = "";

        [Phone]
        public string? Phone { get; set; }

        [StringLength(100)]
        public string? CompanyName { get; set; }

        public UserType UserType { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Admin create user form
    /// </summary>
    public class AdminCreateUserViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = "";

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = "";

        [Phone]
        public string? Phone { get; set; }

        [StringLength(100)]
        public string? CompanyName { get; set; }

        public UserType UserType { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = "";

        public bool SendInvitation { get; set; } = false;
    }

    /// <summary>
    /// Admin delete user confirmation
    /// </summary>
    public class AdminDeleteUserViewModel
    {
        public string UserId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public UserType UserType { get; set; }
        public int ProjectCount { get; set; }
        public int UnitCount { get; set; }
        public int AssignmentCount { get; set; }
        public bool HasAnyActivity { get; set; }
        public bool CanDelete => !HasAnyActivity;
    }

    /// <summary>
    /// Admin set builder quotas
    /// </summary>
    public class AdminSetBuilderQuotaViewModel
    {
        public string UserId { get; set; } = "";
        public string BuilderName { get; set; } = "";
        public int MaxProjects { get; set; }
        public List<AdminProjectQuotaItem> Projects { get; set; } = new();
    }

    /// <summary>
    /// Per-project quota item for builder quota form
    /// </summary>
    public class AdminProjectQuotaItem
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = "";
        public int CurrentUnitCount { get; set; }
        public int? MaxUnits { get; set; }
    }

    /// <summary>
    /// Admin reset password form
    /// </summary>
    public class AdminResetPasswordViewModel
    {
        public string UserId { get; set; } = "";
        public string Email { get; set; } = "";
        public string FullName { get; set; } = "";

        [Required]
        [StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = "";
    }

    #endregion

    #region Marketing Agency

    // ===== MARKETING AGENCY DASHBOARD =====
    public class MarketingAgencyDashboardViewModel
    {
        public List<MarketingAgencyProjectItemViewModel> Projects { get; set; } = new();
    }

    public class MarketingAgencyProjectItemViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public ProjectStatus Status { get; set; }
        public int TotalUnits { get; set; }
        public int UnitsNeedingDiscount { get; set; }
        public DateTime? ClosingDate { get; set; }

        public string StatusBadgeClass => Status switch
        {
            ProjectStatus.Active => "bg-success",
            ProjectStatus.Closing => "bg-warning",
            ProjectStatus.Completed => "bg-secondary",
            ProjectStatus.Draft => "bg-info",
            _ => "bg-secondary"
        };
    }

    // ===== MARKETING AGENCY PROJECT UNITS VIEW =====
    // Design/pricing view only — no SOA, mortgage, or financial data (spec Section H)
    public class MarketingAgencyProjectUnitsViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public List<MarketingAgencyUnitItemViewModel> Units { get; set; } = new();
    }

    public class MarketingAgencyUnitItemViewModel
    {
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = string.Empty;
        public UnitType UnitType { get; set; }
        public int Bedrooms { get; set; }
        public int Bathrooms { get; set; }
        public decimal SquareFootage { get; set; }
        public decimal PurchasePrice { get; set; }
        public DateTime? ClosingDate { get; set; }

        // AI recommendation category — colour-coded badge only; no raw SOA/shortfall amounts
        public ClosingRecommendation? Recommendation { get; set; }

        // AI suggested discount (from ShortfallAnalysis) — visible so MA can calibrate their suggestion
        public decimal? AISuggestedDiscount { get; set; }

        // Has this MA user already submitted a suggestion for this unit?
        public bool HasMASuggestion { get; set; }
        public string? MASuggestionJson { get; set; } // Raw NewValues JSON from AuditLog

        public string RecommendationBadgeClass => Recommendation switch
        {
            ClosingRecommendation.ProceedToClose        => "bg-success",
            ClosingRecommendation.CloseWithDiscount     => "bg-lightgreen",
            ClosingRecommendation.VTBSecondMortgage     => "bg-lightyellow",
            ClosingRecommendation.VTBFirstMortgage      => "bg-orange",
            ClosingRecommendation.HighRiskDefault       => "bg-danger",
            ClosingRecommendation.PotentialDefault      => "bg-danger",
            ClosingRecommendation.MutualRelease         => "bg-purple",
            ClosingRecommendation.CombinationSuggestion => "bg-combination",
            _                                           => "bg-secondary"
        };

        public string RecommendationText => Recommendation switch
        {
            ClosingRecommendation.ProceedToClose        => "Proceed to Close",
            ClosingRecommendation.CloseWithDiscount     => "Needs Discount",
            ClosingRecommendation.VTBSecondMortgage     => "VTB 2nd Mortgage",
            ClosingRecommendation.VTBFirstMortgage      => "VTB 1st Mortgage",
            ClosingRecommendation.HighRiskDefault       => "High Risk",
            ClosingRecommendation.PotentialDefault      => "Potential Default",
            ClosingRecommendation.MutualRelease         => "Mutual Release",
            ClosingRecommendation.CombinationSuggestion => "Combination",
            _                                           => "Pending"
        };
    }

    // ===== SUGGEST DISCOUNT (Marketing Agency) =====
    public class SuggestDiscountViewModel
    {
        public int UnitId { get; set; }
        public int ProjectId { get; set; }
        public string UnitNumber { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter a suggested discount amount.")]
        [Range(0, 9999999, ErrorMessage = "Amount must be between 0 and 9,999,999.")]
        [Display(Name = "Suggested Discount / Credit Amount")]
        [DataType(DataType.Currency)]
        public decimal SuggestedAmount { get; set; }

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
        [Display(Name = "Notes / Justification")]
        public string? Notes { get; set; }
    }

    #endregion

    #region Admin Fee Schedule

    public class FeeScheduleViewModel
    {
        public List<SystemFeeConfig> Fees { get; set; } = new();
    }

    public class SystemFeeConfigEditModel
    {
        public int Id { get; set; }

        [Required]
        [Range(0, 999999.99, ErrorMessage = "Amount must be between $0 and $999,999.99.")]
        [DataType(DataType.Currency)]
        [Display(Name = "Base Amount")]
        public decimal Amount { get; set; }

        [Display(Name = "HST Applicable (+13%)")]
        public bool HSTApplicable { get; set; }

        [Display(Name = "HST Already Included")]
        public bool HSTIncluded { get; set; }

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
        [Display(Name = "Notes / Source")]
        public string? Notes { get; set; }
    }

    #endregion

    #region Project Investment

    public class ProjectInvestmentViewModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = "";

        [Display(Name = "Total Revenue ($)")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalRevenue { get; set; }

        [Display(Name = "Total Investment ($)")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalInvestment { get; set; }

        [Display(Name = "Marketing Cost ($)")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MarketingCost { get; set; }

        [Display(Name = "Profit Available ($)")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ProfitAvailable { get; set; }

        [Display(Name = "Max Builder Capital for VTB ($)")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MaxBuilderCapital { get; set; }

        [StringLength(500)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        // Read-only summary from project
        public int TotalUnits { get; set; }
        public int UnsoldUnits { get; set; }
        public decimal CalculatedProfitPerUnit => UnsoldUnits > 0 ? ProfitAvailable / UnsoldUnits : 0;
        public decimal CalculatedVTBPerUnit => UnsoldUnits > 0 ? MaxBuilderCapital / UnsoldUnits : 0;
    }

    #endregion

    #region SOA Version History ViewModels

    public class SOAVersionHistoryViewModel
    {
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public List<SOAVersionItem> Versions { get; set; } = new();
    }

    public class SOAVersionItem
    {
        public int Id { get; set; }
        public int VersionNumber { get; set; }
        public string Source { get; set; } = "";
        public string SourceBadgeClass { get; set; } = "bg-secondary";
        public decimal BalanceDueOnClosing { get; set; }
        public decimal TotalVendorCredits { get; set; }
        public decimal TotalPurchaserCredits { get; set; }
        public decimal CashRequiredToClose { get; set; }
        public string? UploadedFilePath { get; set; }
        public string CreatedByName { get; set; } = "";
        public string CreatedByRole { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string? Notes { get; set; }

        public string SourceDisplayName => Source switch
        {
            "SystemCalculation" => "System Calculation",
            "LawyerUpload" => "Lawyer Upload",
            "BuilderUpload" => "Builder Upload",
            _ => Source
        };
    }

    #endregion

}
