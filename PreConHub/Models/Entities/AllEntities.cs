using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using PreConHub.Models.ViewModels;

namespace PreConHub.Models.Entities
{
    #region User & Authentication

    /// <summary>
    /// Extended user class with role-specific properties
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? CompanyName { get; set; }

        [StringLength(200)]
        public string? LawFirm { get; set; }

        [StringLength(20)]
        public string? CellPhone { get; set; }

        public UserType UserType { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastLoginAt { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>Maximum number of projects this builder can create. Default 1 for new builders. Admin-adjustable.</summary>
        public int MaxProjects { get; set; } = 1;

        /// <summary>ID of the Admin/Builder who created this user</summary>
        public string? CreatedByUserId { get; set; }
        public virtual ApplicationUser? CreatedByUser { get; set; }

        // Navigation properties
        public virtual ICollection<Project> BuilderProjects { get; set; } = new List<Project>();
        public virtual ICollection<UnitPurchaser> PurchaserUnits { get; set; } = new List<UnitPurchaser>();
        public virtual ICollection<LawyerAssignment> LawyerAssignments { get; set; } = new List<LawyerAssignment>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }

    public enum UserType
    {
        PlatformAdmin = 0,  // ← admin@preconhub.ca, afshin@preconhub.com
        Builder = 1,        // ← builder@test.com (after SQL fix)
        Purchaser = 2,      // ← john.smith@test.com
        Lawyer = 3,         // ← sarah.lawyer, sir.alex
        MarketingAgency = 4
    }

    #endregion

    #region Project

    /// <summary>
    /// Represents a pre-construction development project
    /// </summary>
    public class Project
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Address { get; set; } = string.Empty;

        [StringLength(100)]
        public string City { get; set; } = "Toronto";

        [StringLength(10)]
        public string PostalCode { get; set; } = string.Empty;

        public ProjectType ProjectType { get; set; }

        public int TotalUnits { get; set; }

        public DateTime? OccupancyDate { get; set; }

        public DateTime? ClosingDate { get; set; }

        public ProjectStatus Status { get; set; } = ProjectStatus.Active;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Builder relationship
        [Required]
        public string BuilderId { get; set; } = string.Empty;

        [ForeignKey("BuilderId")]
        public virtual ApplicationUser Builder { get; set; } = null!;

        // Navigation properties
        public virtual ICollection<Unit> Units { get; set; } = new List<Unit>();
        public virtual ICollection<ProjectFee> Fees { get; set; } = new List<ProjectFee>();
        public virtual ICollection<LawyerAssignment> LawyerAssignments { get; set; } = new List<LawyerAssignment>();
        public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
        public string? BuilderCompanyName { get; set; }
        public virtual ICollection<ProjectLevyCap> LevyCaps { get; set; } = new List<ProjectLevyCap>();

        // =====================================
        // APS / Tarion — Project-Level Data
        // =====================================
        /// <summary>Tarion enrolment/registration number for this project.</summary>
        [StringLength(50)]
        public string? TarionRegistrationNumber { get; set; }

        /// <summary>Legal description of the property (e.g., "Lot 35 and Lot 36 on Plan 806, City of Toronto").</summary>
        [StringLength(500)]
        public string? PropertyLegalDescription { get; set; }

        /// <summary>Vendor's solicitor — firm name.</summary>
        [StringLength(200)]
        public string? VendorSolicitorName { get; set; }

        /// <summary>Vendor's solicitor — address.</summary>
        [StringLength(500)]
        public string? VendorSolicitorAddress { get; set; }

        /// <summary>Vendor's solicitor — phone.</summary>
        [StringLength(50)]
        public string? VendorSolicitorPhone { get; set; }

        /// <summary>Vendor's solicitor — email.</summary>
        [StringLength(200)]
        public string? VendorSolicitorEmail { get; set; }

        /// <summary>Date construction commenced (Tarion addendum).</summary>
        public DateTime? CommencementOfConstructionDate { get; set; }

        // Marketing Agency conditional access (spec Section H)
        public bool AllowMarketingAccess { get; set; } = false;

        /// <summary>Maximum units allowed in this project. Null = quota not set (blocked). Admin-adjustable.</summary>
        public int? MaxUnits { get; set; }

        // Per-project Marketing Agency assignment (spec Section H)
        public string? MarketingAgencyUserId { get; set; }

        [ForeignKey("MarketingAgencyUserId")]
        public virtual ApplicationUser? MarketingAgencyUser { get; set; }

        // Builder-only project financials (spec Section E)
        public virtual ProjectFinancials? Financials { get; set; }
    }

    public enum ProjectType
    {
        Condominium = 0,
        Townhouse = 1,
        Detached = 2,
        SemiDetached = 3,
        Stacked = 4
    }

    public enum ProjectStatus
    {
        Draft = 0,
        Active = 1,
        Closing = 2,
        Completed = 3,
        Cancelled = 4
    }

    #endregion

    #region Project Fees (Levies & Fees set by Builder)

    /// <summary>
    /// Project-level fees that apply to all or specific units
    /// </summary>
    public class ProjectFee
    {
        [Key]
        public int Id { get; set; }

        public int ProjectId { get; set; }

        [ForeignKey("ProjectId")]
        public virtual Project Project { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string FeeName { get; set; } = string.Empty;

        public FeeType FeeType { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public bool IsPercentage { get; set; } = false;

        public bool AppliesToAllUnits { get; set; } = true;

        [StringLength(500)]
        public string? Description { get; set; }

        public bool IsConfirmedByLawyer { get; set; } = false;

        public DateTime? ConfirmedAt { get; set; }

        public string? ConfirmedByLawyerId { get; set; }
    }

    public enum FeeType
    {
        LandTransferTax = 0,
        TorontoLandTransferTax = 1,
        DevelopmentCharges = 2,
        TarionWarranty = 3,
        UtilityConnection = 4,
        LegalFees = 5,
        ParkingUpgrade = 6,
        LockerUpgrade = 7,
        EducationDevelopmentCharges = 8,    // EDCs
        ParklandLevy = 9,
        CommunityBenefitCharges = 10,       // CBC
        Section37Contribution = 11,
        Section42Contribution = 12,
        SewerConnectionFee = 13,
        WaterConnectionFee = 14,
        HydroConnectionFee = 15,
        GasConnectionFee = 16,
        TelecomConnectionFee = 17,
        TarionAdjustmentFee = 18,
        MeterInstallationFee = 19,

        // APS Schedule B Part I — Stipulated Closing Fees
        ChequeAdministrationFee = 20,       // APS 5(a) — $300 + HST
        PartialDischargeFee = 21,           // APS 6(d)(viii) — $350 + HST
        PDIFee = 22,                        // APS 6(d)(x) — $250 + HST
        EngineeringReportFee = 23,          // APS 6(d)(xi) — $350 + HST
        InternetDeliveryFee = 24,           // APS 6(d)(xii) — $150 + HST
        CarbonMonoxideDetectorFee = 25,     // APS 6(d)(xiii) — up to $150 + HST
        WireTransferFee = 26,               // APS 6(j) — $150 + HST per transaction
        PDFScanFee = 27,                    // APS 6(k) — $150 + HST
        ClosingDocChangesFee = 28,          // APS 6(l) — $500 + HST

        Other = 99
    }

    #endregion

    #region Unit

    /// <summary>
    /// Individual unit within a project
    /// </summary>
    public class Unit
    {
        [Key]
        public int Id { get; set; }

        public int ProjectId { get; set; }

        [ForeignKey("ProjectId")]
        public virtual Project Project { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string UnitNumber { get; set; } = string.Empty;

        [StringLength(50)]
        public string? FloorNumber { get; set; }

        public UnitType UnitType { get; set; }

        public int Bedrooms { get; set; }

        public int Bathrooms { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal SquareFootage { get; set; }

        // Financial - from APS (Agreement of Purchase and Sale)
        [Column(TypeName = "decimal(18,2)")]
        public decimal PurchasePrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CurrentAppraisalValue { get; set; }

        public DateTime? AppraisalDate { get; set; }

        // Parking & Locker
        public bool HasParking { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ParkingPrice { get; set; } = 0;

        public bool HasLocker { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LockerPrice { get; set; } = 0;

        // Dates
        public DateTime? OccupancyDate { get; set; }

        public DateTime? ClosingDate { get; set; }

        public DateTime? FirmClosingDate { get; set; }

        // Status
        public UnitStatus Status { get; set; } = UnitStatus.Pending;

        public ClosingRecommendation? Recommendation { get; set; }

        public BuilderDecision? BuilderDecision { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Lawyer confirmation
        public bool IsConfirmedByLawyer { get; set; } = false;

        public DateTime? ConfirmedAt { get; set; }

        public string? ConfirmedByLawyerId { get; set; }

        // Navigation properties
        public virtual ICollection<UnitPurchaser> Purchasers { get; set; } = new List<UnitPurchaser>();
        public virtual ICollection<Deposit> Deposits { get; set; } = new List<Deposit>();
        public virtual ICollection<UnitFee> Fees { get; set; } = new List<UnitFee>();
        public virtual ICollection<OccupancyFee> OccupancyFees { get; set; } = new List<OccupancyFee>();
        public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
        public virtual StatementOfAdjustments? SOA { get; set; }
        public virtual ShortfallAnalysis? ShortfallAnalysis { get; set; }
        public bool LawyerConfirmed { get; set; }
        public DateTime? LawyerConfirmedAt { get; set; }
        public bool BuyerLawyerConfirmed { get; set; }
        public DateTime? BuyerLawyerConfirmedAt { get; set; }
        public virtual ICollection<LawyerAssignment> LawyerAssignments { get; set; } = new List<LawyerAssignment>();
        public DateTime? APSDate { get; set; }
        public DateTime? InterimOccupancyStartDate { get; set; }
        public bool IsFirstTimeBuyer { get; set; } = false;
        public bool IsPrimaryResidence { get; set; } = true;
        /// <summary>Actual annual municipal land tax paid by builder — used for accurate SOA land tax adjustment.</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal? ActualAnnualLandTax { get; set; }
        /// <summary>Actual monthly common expense/maintenance fee — used for accurate SOA common expense adjustment.</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal? ActualMonthlyMaintenanceFee { get; set; }
        public virtual ICollection<ClosingExtensionRequest> ExtensionRequests { get; set; } = new List<ClosingExtensionRequest>();
        public virtual ICollection<SOAVersion> SOAVersions { get; set; } = new List<SOAVersion>();

        // =====================================
        // APS / Tarion Addendum — Critical Dates
        // =====================================
        /// <summary>Tarion: first tentative occupancy date from addendum.</summary>
        public DateTime? FirstTentativeOccupancyDate { get; set; }
        /// <summary>Tarion: outside occupancy date — latest allowed date before purchaser can terminate.</summary>
        public DateTime? OutsideOccupancyDate { get; set; }
        /// <summary>Tarion: delayed occupancy date (Section 3).</summary>
        public DateTime? DelayedOccupancyDate { get; set; }
        /// <summary>End of purchaser's termination period after Outside Occupancy Date.</summary>
        public DateTime? PurchaserTerminationDate { get; set; }

        // =====================================
        // Parking & Locker Counts (APS)
        // =====================================
        /// <summary>Number of parking units included in APS (default 0).</summary>
        public int ParkingCount { get; set; }
        /// <summary>Number of storage locker units included in APS (default 0).</summary>
        public int LockerCount { get; set; }

        // =====================================
        // Late Closing Penalty
        // =====================================
        /// <summary>Daily penalty amount charged when closing is late.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? DailyPenaltyAmount { get; set; }
        /// <summary>Whether penalty is currently accruing.</summary>
        public bool IsPenaltyActive { get; set; } = false;
        /// <summary>Date penalty started accruing.</summary>
        public DateTime? PenaltyStartDate { get; set; }
        /// <summary>Date penalty was paused (null if not paused).</summary>
        public DateTime? PenaltyPausedAt { get; set; }
        /// <summary>Running total of all accrued penalties.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAccumulatedPenalty { get; set; }
        /// <summary>Total number of days penalty has been charged.</summary>
        public int PenaltyDaysCount { get; set; }
        /// <summary>Immutable daily penalty accrual records.</summary>
        public virtual ICollection<ClosingPenalty> ClosingPenalties { get; set; } = new List<ClosingPenalty>();
    }

    public enum UnitType
    {
        Studio = 0,
        OneBedroom = 1,
        OnePlusDen = 2,
        TwoBedroom = 3,
        TwoPlusDen = 4,
        ThreeBedroom = 5,
        Penthouse = 6,
        Townhouse = 7,
        Other = 99
    }

    public enum UnitStatus
    {
        Pending = 0,              // Waiting for purchaser data
        DataComplete = 1,         // All data received
        UnderReview = 2,          // Being reviewed
        ReadyToClose = 3,         // Can close as-is
        NeedsDiscount = 4,        // Needs discount to close
        NeedsVTB = 5,             // Needs vendor take-back mortgage
        AtRisk = 6,               // High risk of default
        Closed = 7,               // Successfully closed
        Defaulted = 8,            // Purchaser defaulted
        Cancelled = 9,            // Deal cancelled
        LateClosingPenalty = 10   // Past closing date, daily penalty accruing
    }

    public enum ClosingRecommendation
    {
        ProceedToClose = 0,         // Dark Green - ready to close
        CloseWithDiscount = 1,      // Light Green - small shortfall, offer discount
        VTBSecondMortgage = 2,      // Light Yellow - medium shortfall
        VTBFirstMortgage = 3,       // Orange - large shortfall, credit score >= 700
        HighRiskDefault = 4,        // Red - likely to default
        PotentialDefault = 5,       // Dark Red - very high risk
        MutualRelease = 6,          // Purple - purchaser paid enough to release contract
        CombinationSuggestion = 7   // Yellow - AI splits shortfall across discount + VTB + extension
    }

    public enum BuilderDecision
    {
        None = 0,
        ProceedToClose = 1,
        CloseWithDiscount = 2,
        VTBSecondMortgage = 3,
        VTBFirstMortgage = 4,
        HighRiskDefault = 5,
        PotentialDefault = 6,
        MutualRelease = 7,
        CombinationSuggestion = 8,
        Downsizing = 9,              // Manual-only, not in AI recommendations
        Terminated = 10              // Builder terminated the deal
    }

    #endregion

    #region Unit-Specific Fees

    /// <summary>
    /// Fees specific to a unit (upgrades, amendments)
    /// </summary>
    public class UnitFee
    {
        [Key]
        public int Id { get; set; }

        public int UnitId { get; set; }

        [ForeignKey("UnitId")]
        public virtual Unit Unit { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string FeeName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public bool IsCredit { get; set; } = false; // True if this is a credit to purchaser

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    #endregion

    #region Purchaser & Mortgage

    /// <summary>
    /// Links purchasers to units (supports multiple purchasers per unit)
    /// </summary>
    public class UnitPurchaser
    {
        [Key]
        public int Id { get; set; }

        public int UnitId { get; set; }

        [ForeignKey("UnitId")]
        public virtual Unit Unit { get; set; } = null!;

        public string PurchaserId { get; set; } = string.Empty;

        [ForeignKey("PurchaserId")]
        public virtual ApplicationUser Purchaser { get; set; } = null!;

        public bool IsPrimaryPurchaser { get; set; } = true;

        [Column(TypeName = "decimal(5,2)")]
        public decimal OwnershipPercentage { get; set; } = 100;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Purchaser's financial info
        public virtual MortgageInfo? MortgageInfo { get; set; }
        public virtual PurchaserFinancials? Financials { get; set; }
    }

    /// <summary>
    /// Mortgage information submitted by purchaser
    /// </summary>
    public class MortgageInfo
    {
        [Key]
        public int Id { get; set; }

        public int UnitPurchaserId { get; set; }

        [ForeignKey("UnitPurchaserId")]
        public virtual UnitPurchaser UnitPurchaser { get; set; } = null!;

        public bool HasMortgageApproval { get; set; }

        public MortgageApprovalType ApprovalType { get; set; }

        [StringLength(100)]
        public string? MortgageProvider { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ApprovedAmount { get; set; }

        [Column(TypeName = "decimal(5,3)")]
        public decimal? InterestRate { get; set; }

        public int? AmortizationYears { get; set; }

        public DateTime? ApprovalDate { get; set; }

        public DateTime? ApprovalExpiryDate { get; set; }

        public bool IsApprovalConfirmed { get; set; } = false;

        // Visibility - Builder can see limited info
        public bool BuilderCanViewAmount { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public bool HasConditions { get; set; }

        public string? Conditions { get; set; }

        // Is this a blanket mortgage? (spec Section A.2)
        public bool IsBlanketMortgage { get; set; } = false;

        // Appraised value reported by purchaser (used when no blanket mortgage)
        // Distinct from Unit.CurrentAppraisalValue which is set by builder
        [Column(TypeName = "decimal(18,2)")]
        public decimal? PurchaserAppraisalValue { get; set; }

        // Estimated mortgage funding / closing date (spec Section A.2)
        public DateTime? EstimatedFundingDate { get; set; }

        // Credit score — key gate for VTB 1st Mortgage vs Default tier (spec AI Section 3)
        public int? CreditScore { get; set; }

        // Credit bureau used when no mortgage approval (TransUnion / Equifax)
        [StringLength(50)]
        public string? CreditBureau { get; set; }

        // Free-text comments / notes from purchaser (spec Section A.2)
        public string? Comments { get; set; }
    }

    public enum MortgageApprovalType
    {
        None = 0,
        PreApproval = 1,
        FirmApproval = 2,
        Blanket = 3,  // Blanket approval covering multiple properties
        Conditional = 4
    }

    /// <summary>
    /// Additional financial info from purchaser
    /// </summary>
    public class PurchaserFinancials
    {
        [Key]
        public int Id { get; set; }

        public int UnitPurchaserId { get; set; }

        [ForeignKey("UnitPurchaserId")]
        public virtual UnitPurchaser UnitPurchaser { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? AnnualIncome { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? AdditionalCashAvailable { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? OtherAssetsValue { get; set; }

        [StringLength(500)]
        public string? IncomeSource { get; set; }

        public EmploymentStatus EmploymentStatus { get; set; }

        [StringLength(100)]
        public string? Employer { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public decimal RRSPAvailable { get; set; }
        public decimal GiftFromFamily { get; set; }
        public decimal ProceedsFromSale { get; set; }
        public string? OtherFundsDescription { get; set; }
        public decimal OtherFundsAmount { get; set; }
        public decimal TotalFundsAvailable { get; set; }
        public bool HasExistingPropertyToSell { get; set; }
        public decimal? ExistingPropertyValue { get; set; }
        public decimal? ExistingMortgageBalance { get; set; }
        public bool IsPropertyListed { get; set; }
        public DateTime? ExpectedSaleDate { get; set; }
    }

    public enum EmploymentStatus
    {
        Employed = 0,
        SelfEmployed = 1,
        Retired = 2,
        Unemployed = 3,
        Other = 4
    }

    #endregion

    #region Deposits

    /// <summary>
    /// Deposit payments made by purchaser
    /// </summary>
    public class Deposit
    {
        [Key]
        public int Id { get; set; }

        public int UnitId { get; set; }

        [ForeignKey("UnitId")]
        public virtual Unit Unit { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string DepositName { get; set; } = string.Empty; // e.g., "Initial Deposit", "Second Deposit"

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public DateTime DueDate { get; set; }

        public DateTime? PaidDate { get; set; }

        public bool IsPaid { get; set; } = false;

        public DepositStatus Status { get; set; } = DepositStatus.Pending;

        [StringLength(100)]
        public string? PaymentMethod { get; set; }

        [StringLength(100)]
        public string? ReferenceNumber { get; set; }

        // Lawyer confirmation
        public bool IsConfirmedByLawyer { get; set; } = false;

        public DateTime? ConfirmedAt { get; set; }

        public string? ConfirmedByLawyerId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Deposit as percentage of purchase price (e.g. 5.0 = 5%). From APS deposit schedule.</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal? DepositPercentage { get; set; }

        public DepositHolder Holder { get; set; } = DepositHolder.Builder;
        public bool IsInterestEligible { get; set; } = false;
        [Column(TypeName = "decimal(5,3)")]
        public decimal? InterestRate { get; set; }
        public InterestCompoundingType CompoundingType { get; set; } = InterestCompoundingType.Simple;
        public virtual ICollection<DepositInterestPeriod> InterestPeriods { get; set; } = new List<DepositInterestPeriod>();
    }

    /// <summary>Government-published semi-annual interest rate period for a deposit (daily simple interest per period).</summary>
    public class DepositInterestPeriod
    {
        [Key] public int Id { get; set; }
        public int DepositId { get; set; }
        [ForeignKey("DepositId")] public virtual Deposit Deposit { get; set; } = null!;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        /// <summary>Annual rate as a percentage, e.g. 1.500 = 1.5% per annum.</summary>
        [Column(TypeName = "decimal(6,3)")] public decimal AnnualRate { get; set; }
    }

    public enum DepositStatus
    {
        Pending = 0,
        Paid = 1,
        Late = 2,
        Refunded = 3,
        Forfeited = 4
    }
    public enum DepositHolder { Builder = 0, Trust = 1, Lawyer = 2 }
    public enum InterestCompoundingType { Simple = 0, Annual = 1, Monthly = 2 }

    #endregion

    #region Occupancy Fees

    /// <summary>
    /// Monthly occupancy fees (interim occupancy period)
    /// </summary>
    public class OccupancyFee
    {
        [Key]
        public int Id { get; set; }

        public int UnitId { get; set; }

        [ForeignKey("UnitId")]
        public virtual Unit Unit { get; set; } = null!;

        public DateTime PeriodStart { get; set; }

        public DateTime PeriodEnd { get; set; }

        // Components of occupancy fee
        [Column(TypeName = "decimal(18,2)")]
        public decimal InterestComponent { get; set; }  // Interest on unpaid balance

        [Column(TypeName = "decimal(18,2)")]
        public decimal PropertyTaxComponent { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CommonExpenseComponent { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalMonthlyFee { get; set; }

        public bool IsPaid { get; set; } = false;

        public DateTime? PaidDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    #endregion

    #region Statement of Adjustments (SOA)

    /// <summary>
    /// Calculated Statement of Adjustments for a unit
    /// </summary>
    public class StatementOfAdjustments
    {
        [Key]
        public int Id { get; set; }

        public int UnitId { get; set; }

        [ForeignKey("UnitId")]
        public virtual Unit Unit { get; set; } = null!;

        // Debits (amounts purchaser owes)
        [Column(TypeName = "decimal(18,2)")]
        public decimal PurchasePrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LandTransferTax { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TorontoLandTransferTax { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DevelopmentCharges { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TarionFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UtilityConnectionFees { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PropertyTaxAdjustment { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CommonExpenseAdjustment { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OccupancyFeesOwing { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ParkingPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LockerPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Upgrades { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LegalFeesEstimate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OtherDebits { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalDebits { get; set; }

        // Credits (amounts reducing purchaser's obligation)
        [Column(TypeName = "decimal(18,2)")]
        public decimal DepositsPaid { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DepositInterest { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BuilderCredits { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OtherCredits { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCredits { get; set; }

        // Final calculations
        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceDueOnClosing { get; set; }  // TotalDebits - TotalCredits

        [Column(TypeName = "decimal(18,2)")]
        public decimal MortgageAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CashRequiredToClose { get; set; }  // BalanceDueOnClosing - MortgageAmount

        // Calculation metadata
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? RecalculatedAt { get; set; }

        public int CalculationVersion { get; set; } = 1;

        // Lawyer confirmation
        public bool IsConfirmedByLawyer { get; set; } = false;

        public DateTime? ConfirmedAt { get; set; }

        public string? ConfirmedByLawyerId { get; set; }

        [StringLength(1000)]
        public string? LawyerNotes { get; set; }

        // Buyer's lawyer confirmation
        public bool IsConfirmedByBuyerLawyer { get; set; } = false;
        public DateTime? BuyerLawyerConfirmedAt { get; set; }
        public string? ConfirmedByBuyerLawyerId { get; set; }
        [StringLength(1000)]
        public string? BuyerLawyerNotes { get; set; }

        /// <summary>Balance due on closing entered by the lawyer from their uploaded SOA document. Null = not yet uploaded.</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal? LawyerUploadedBalanceDue { get; set; }

        // =====================================
        // Net Sale Price Breakdown
        // =====================================

        /// <summary>Dwelling + Parking + Locker (before fees/HST)</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal SalePrice { get; set; }

        /// <summary>Sum of all fee items × 1.13 (each with HST component)</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal AdditionalConsideration { get; set; }

        /// <summary>SalePrice + AdditionalConsideration</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal TotalSalePrice { get; set; }

        /// <summary>(TotalSalePrice + TotalHSTRebates) / 1.13 — the net amount before HST</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal NetSalePrice { get; set; }

        /// <summary>NetSalePrice × 5% (federal GST portion)</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal FederalHST { get; set; }

        /// <summary>NetSalePrice × 8% (provincial PST portion)</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal ProvincialHST { get; set; }

        /// <summary>2 × monthly common expenses — reserve fund contribution on closing</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal ReserveFundContribution { get; set; }

        /// <summary>First month common expenses after closing</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal CommonExpensesFirstMonth { get; set; }

        // =====================================
        // NEW: HST & REBATE FIELDS (Critical)
        // =====================================

        /// <summary>
        /// Total HST on purchase (13% in Ontario)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal HSTAmount { get; set; }

        /// <summary>
        /// Is purchaser eligible for HST rebate (New Housing Rebate)
        /// Eligible if: primary residence AND price under $450,000 (full) or $450,000-$682,500 (partial)
        /// </summary>
        public bool IsHSTRebateEligible { get; set; }

        /// <summary>
        /// Federal portion of rebate (max $6,300)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal HSTRebateFederal { get; set; }

        /// <summary>
        /// Ontario portion of rebate (max $24,000)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal HSTRebateOntario { get; set; }

        /// <summary>
        /// Total HST rebate amount
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal HSTRebateTotal { get; set; }

        /// <summary>
        /// Is rebate assigned to builder? (Common in pre-construction)
        /// If true, builder receives rebate directly; net HST payable by buyer is reduced
        /// </summary>
        public bool IsHSTRebateAssignedToBuilder { get; set; } = true;

        /// <summary>
        /// Net HST payable by purchaser after rebate
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal NetHSTPayable { get; set; }

        // =====================================
        // NEW: Additional Levy Details
        // =====================================

        [Column(TypeName = "decimal(18,2)")]
        public decimal EducationDevelopmentCharges { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ParklandLevy { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CommunityBenefitCharges { get; set; }

        /// <summary>
        /// Amount builder absorbs due to levy caps
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal BuilderAbsorbedLevies { get; set; }

        /// <summary>APS Schedule B Part I closing fees aggregate: cheque admin, partial discharge, PDI,
        /// engineering report, internet delivery, CO detector, wire transfer, PDF scan, closing doc changes.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal ScheduleBClosingFees { get; set; }

        /// <summary>Late closing penalty total — contractual daily penalty, no HST. Added to TotalVendorCredits.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal LatePenalties { get; set; }

        // =====================================
        // NEW: Credits & Incentives Detail
        // =====================================

        [Column(TypeName = "decimal(18,2)")]
        public decimal DesignCredits { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal FreeUpgradesValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CashBackIncentives { get; set; }

        // =====================================
        // Priority 6 — Real-World SOA Alignment Fields
        // =====================================

        /// <summary>HCRA Regulatory Oversight Fee (Credit Vendor) — loaded from SystemFeeConfig.</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal HCRAFee { get; set; }
        /// <summary>Electronic Registration Fee / Teranet (Credit Vendor) — loaded from SystemFeeConfig.</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal ElectronicRegFee { get; set; }
        /// <summary>Status Certificate fee (Credit Vendor) — loaded from SystemFeeConfig.</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal StatusCertFee { get; set; }
        /// <summary>Transaction Levy Surcharge / LAWPRO (Credit Vendor) — loaded from SystemFeeConfig.</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal TransactionLevyFee { get; set; }
        /// <summary>Security deposit refund to purchaser (Credit Purchaser).</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal SecurityDepositRefund { get; set; }
        /// <summary>Occupancy fees chargeable to purchaser from occupancy to day before closing (Credit Vendor).</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal OccupancyFeesChargeable { get; set; }
        /// <summary>Occupancy fees actually paid by purchaser during the occupancy period (Credit Purchaser).</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal OccupancyFeesPaid { get; set; }
        /// <summary>Interest earned on the total deposit interest, from OccupancyDate to ClosingDate (Credit Purchaser).</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal InterestOnDepositInterest { get; set; }
        /// <summary>Sum of all Credit Vendor items — replaces TotalDebits in the two-column SOA model.</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal TotalVendorCredits { get; set; }
        /// <summary>Sum of all Credit Purchaser items — replaces TotalCredits in the two-column SOA model.</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal TotalPurchaserCredits { get; set; }

        // =====================================
        // Lawyer SOA Confirmation (lawyer values override system)
        // =====================================

        /// <summary>Has the lawyer-uploaded SOA been confirmed? When true, lawyer's BalanceDueOnClosing overrides the system calculation.</summary>
        public bool IsLawyerSOAConfirmed { get; set; } = false;

        /// <summary>When the lawyer SOA was confirmed.</summary>
        public DateTime? LawyerSOAConfirmedAt { get; set; }

        /// <summary>User who confirmed the lawyer SOA.</summary>
        public string? LawyerSOAConfirmedByUserId { get; set; }

        /// <summary>Role of the user who confirmed (Builder or Lawyer).</summary>
        [StringLength(50)]
        public string? LawyerSOAConfirmedByRole { get; set; }

        /// <summary>System-calculated BalanceDueOnClosing before lawyer override. Preserved for audit.</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal? SystemBalanceDueOnClosing { get; set; }

        /// <summary>System-calculated CashRequiredToClose before lawyer override. Preserved for audit.</summary>
        [Column(TypeName = "decimal(18,2)")] public decimal? SystemCashRequiredToClose { get; set; }

        // =====================================
        // NEW: Locking & Audit Fields
        // =====================================

        /// <summary>
        /// Is SOA locked (confirmed by both builder and lawyer)
        /// </summary>
        public bool IsLocked { get; set; } = false;

        /// <summary>
        /// When was SOA locked
        /// </summary>
        public DateTime? LockedAt { get; set; }

        /// <summary>
        /// Who locked the SOA
        /// </summary>
        public string? LockedByUserId { get; set; }

        /// <summary>
        /// Builder confirmation
        /// </summary>
        public bool IsConfirmedByBuilder { get; set; } = false;

        public DateTime? BuilderConfirmedAt { get; set; }

        public string? ConfirmedByBuilderId { get; set; }
    }


    #endregion

    #region Shortfall Analysis

    /// <summary>
    /// Shortfall calculation and AI recommendation
    /// </summary>
    public class ShortfallAnalysis
    {
        [Key]
        public int Id { get; set; }

        public int UnitId { get; set; }

        [ForeignKey("UnitId")]
        public virtual Unit Unit { get; set; } = null!;

        // From SOA
        [Column(TypeName = "decimal(18,2)")]
        public decimal SOAAmount { get; set; }

        // From Purchaser
        [Column(TypeName = "decimal(18,2)")]
        public decimal MortgageApproved { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DepositsPaid { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AdditionalCashAvailable { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalFundsAvailable { get; set; }

        // Shortfall calculation
        [Column(TypeName = "decimal(18,2)")]
        public decimal ShortfallAmount { get; set; }  // SOAAmount - TotalFundsAvailable

        [Column(TypeName = "decimal(5,2)")]
        public decimal ShortfallPercentage { get; set; }  // (Shortfall / PurchasePrice) * 100

        // Risk assessment
        public RiskLevel RiskLevel { get; set; }

        public ClosingRecommendation Recommendation { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? SuggestedDiscount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? SuggestedVTBAmount { get; set; }

        // AI reasoning
        [StringLength(2000)]
        public string? AIAnalysis { get; set; }

        [StringLength(1000)]
        public string? RecommendationReasoning { get; set; }

        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? RecalculatedAt { get; set; }

        // Mutual release threshold (spec AI Step 2)
        // Formula: APS_Unit - ((APS_Unit - AppraisedValue) / 3)
        [Column(TypeName = "decimal(18,2)")]
        public decimal? MutualReleaseThreshold { get; set; }

        // Builder decision on AI suggestion (spec Section G)
        // Values: "Accepted", "Modified", "Rejected"
        [StringLength(20)]
        public string? DecisionAction { get; set; }

        public string? DecisionByBuilderId { get; set; }

        public DateTime? DecisionAt { get; set; }

        // Freetext if builder modifies the AI suggestion
        [StringLength(1000)]
        public string? BuilderModifiedSuggestion { get; set; }
    }

    public enum RiskLevel
    {
        Low = 0,
        Medium = 1,
        High = 2,
        VeryHigh = 3
    }

    #endregion

    #region Lawyer Assignment

    /// <summary>
    /// Assigns lawyers to projects for review/confirmation
    /// </summary>
    public class LawyerAssignment
    {
        [Key]
        public int Id { get; set; }

        public int ProjectId { get; set; }
        public int? UnitId { get; set; }

        [ForeignKey("ProjectId")]
        public virtual Project Project { get; set; } = null!;

        public string LawyerId { get; set; } = string.Empty;

        [ForeignKey("LawyerId")]
        public virtual ApplicationUser Lawyer { get; set; } = null!;

        public LawyerRole Role { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public LawyerReviewStatus ReviewStatus { get; set; } = LawyerReviewStatus.Pending;
        public DateTime? ReviewedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public virtual Unit? Unit { get; set; }
        public virtual ICollection<LawyerNote> LawyerNotes { get; set; } = new List<LawyerNote>();
    }

    public enum LawyerRole
    {
        BuilderLawyer = 0,
        PurchaserLawyer = 1,
        ReviewingLawyer = 2
    }

    #endregion

    #region Documents

    /// <summary>
    /// Uploaded documents for projects and units
    /// </summary>
    public class Document
    {
        [Key]
        public int Id { get; set; }

        public int? ProjectId { get; set; }

        [ForeignKey("ProjectId")]
        public virtual Project? Project { get; set; }

        public int? UnitId { get; set; }

        [ForeignKey("UnitId")]
        public virtual Unit? Unit { get; set; }

        [Required]
        [StringLength(200)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [StringLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public DocumentType DocumentType { get; set; }

        public DocumentSource Source { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public string UploadedById { get; set; } = string.Empty;

        [ForeignKey("UploadedById")]
        public virtual ApplicationUser UploadedBy { get; set; } = null!;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        // For Google Drive integration
        [StringLength(500)]
        public string? GoogleDriveFileId { get; set; }

        [StringLength(500)]
        public string? GoogleDriveUrl { get; set; }
    }

    public enum DocumentType
    {
        AgreementOfPurchaseSale = 0,
        Amendment = 1,
        DepositReceipt = 2,
        MortgageApproval = 3,
        Appraisal = 4,
        IdentificationFront = 5,
        IdentificationBack = 6,
        BankStatement = 7,
        EmploymentLetter = 8,
        NOA = 9,  // Notice of Assessment
        SOA = 10,
        TitleSearchReport = 11,
        TitleInsuranceBinder = 12,
        MortgageInstructions = 13,
        DirectionReTitle = 14,
        DirectionReFunds = 15,
        StatutoryDeclaration = 16,
        RequisitionLetter = 17,
        TrustLedger = 18,
        FireInsuranceBinder = 19,
        ClosingReport = 20,
        Other = 99
    }

    public enum DocumentSource
    {
        Builder = 0,
        Purchaser = 1,
        Lawyer = 2,
        Platform = 3
    }

    #endregion

    #region Audit Log

    /// <summary>
    /// Tracks all changes for compliance
    /// </summary>
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string EntityType { get; set; } = string.Empty;

        public int EntityId { get; set; }

        [Required]
        [StringLength(50)]
        public string Action { get; set; } = string.Empty;  // Create, Update, Delete, Confirm, etc.

        public string? UserId { get; set; }

        [StringLength(100)]
        public string? UserName { get; set; }

        [StringLength(50)]
        public string? UserRole { get; set; }

        public string? OldValues { get; set; }  // JSON

        public string? NewValues { get; set; }  // JSON

        [StringLength(100)]
        public string? IpAddress { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Optional free-text notes on the action (spec Section F)
        [StringLength(500)]
        public string? Comments { get; set; }
    }

    #endregion

    #region Project Summary (Calculated/Cached)

    /// <summary>
    /// Cached summary statistics for project dashboard
    /// </summary>
    public class ProjectSummary
    {
        [Key]
        public int Id { get; set; }

        public int ProjectId { get; set; }

        [ForeignKey("ProjectId")]
        public virtual Project Project { get; set; } = null!;

        public int TotalUnits { get; set; }

        public int UnitsReadyToClose { get; set; }

        public int UnitsNeedingDiscount { get; set; }

        public int UnitsNeedingVTB { get; set; }

        public int UnitsAtRisk { get; set; }

        public int UnitsPendingData { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal PercentReadyToClose { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal PercentNeedingDiscount { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal PercentNeedingVTB { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal PercentAtRisk { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalSalesValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalDiscountRequired { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal DiscountPercentOfSales { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalInvestmentAtRisk { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalShortfall { get; set; }

        // Total cash still needed after discount and VTB allocation (spec AI Step 5)
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalFundNeededToClose { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal ClosingProbabilityPercent { get; set; }

        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    }

    #endregion

    #region Lawyer
    public class LawyerNote
    {
        [Key]
        public int Id { get; set; }

        public int LawyerAssignmentId { get; set; }

        [Required]
        public string Note { get; set; } = "";

        public LawyerNoteType NoteType { get; set; }

        /// <summary>
        /// Who can see this note
        /// </summary>
        public NoteVisibility Visibility { get; set; } = NoteVisibility.Internal;

        /// <summary>
        /// Has the builder seen this note (for notes visible to builder)
        /// </summary>
        public bool IsReadByBuilder { get; set; } = false;

        /// <summary>
        /// When builder read the note
        /// </summary>
        public DateTime? ReadByBuilderAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("LawyerAssignmentId")]
        public virtual LawyerAssignment LawyerAssignment { get; set; } = null!;
    }

    /// <summary>
    /// Controls who can see the note
    /// </summary>
    public enum NoteVisibility
    {
        /// <summary>
        /// Only the lawyer who created it can see
        /// </summary>
        Internal = 0,

        /// <summary>
        /// Builder can see (questions, concerns, requests)
        /// </summary>
        ForBuilder = 1,

        /// <summary>
        /// All lawyers on this unit can see (collaboration)
        /// </summary>
        Collaborative = 2,

        /// <summary>
        /// Purchaser can see (buyer's lawyer notes for their client)
        /// </summary>
        ForPurchaser = 3
    }

    public enum LawyerReviewStatus
    {
        Pending = 0,
        UnderReview = 1,
        Approved = 2,
        NeedsRevision = 3
    }

    public enum LawyerNoteType
    {
        General = 0,
        Question = 1,
        Concern = 2,
        RevisionRequest = 3,
        Approval = 4
    }

    #endregion

    /// <summary>
    /// Levy caps defined in APS - caps builder liability for levies
    /// </summary>
    public class ProjectLevyCap
    {
        [Key]
        public int Id { get; set; }

        public int ProjectId { get; set; }

        [ForeignKey("ProjectId")]
        public virtual Project Project { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string LevyName { get; set; } = string.Empty;  // e.g., "Development Charges Cap"

        [Column(TypeName = "decimal(18,2)")]
        public decimal CapAmount { get; set; }

        // Who pays excess over cap: Buyer or Builder
        public ExcessLevyResponsibility ExcessResponsibility { get; set; } = ExcessLevyResponsibility.Builder;

        [StringLength(500)]
        public string? Description { get; set; }

        /// <summary>Comma-separated FeeType enum names this cap covers.
        /// e.g. "DevelopmentCharges,EducationDevelopmentCharges" for a combined DC+EDC cap.
        /// When multiple fee types are listed, the cap applies to their sum.</summary>
        [StringLength(500)]
        public string? CoveredFeeTypes { get; set; }

        /// <summary>Parse CoveredFeeTypes into a list of FeeType enums.</summary>
        [NotMapped]
        public List<FeeType> ParsedFeeTypes
        {
            get
            {
                if (string.IsNullOrWhiteSpace(CoveredFeeTypes)) return new List<FeeType>();
                return CoveredFeeTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => Enum.TryParse<FeeType>(s, out var ft) ? ft : (FeeType?)null)
                    .Where(ft => ft.HasValue)
                    .Select(ft => ft!.Value)
                    .ToList();
            }
        }
    }

    public enum ExcessLevyResponsibility
    {
        Builder = 0,  // Builder absorbs excess
        Buyer = 1     // Buyer pays excess
    }

    #region Notification
    // ============================================================
    // NOTIFICATION ENTITY
    // Add this to AllEntities.cs
    // ============================================================

    /// <summary>
    /// In-app notification for users
    /// </summary>
    public class Notification
    {
        public int Id { get; set; }

        // Recipient
        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        // Notification Content
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Message { get; set; } = string.Empty;

        // Type for icon and styling
        public NotificationType Type { get; set; } = NotificationType.Info;

        // Priority level
        public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;

        // Status
        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }

        // Optional link to navigate to
        [MaxLength(500)]
        public string? ActionUrl { get; set; }

        [MaxLength(50)]
        public string? ActionText { get; set; }

        // Related entities (optional, for context)
        public int? ProjectId { get; set; }
        public int? UnitId { get; set; }
        public string? RelatedUserId { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }

        // For grouping similar notifications
        [MaxLength(100)]
        public string? GroupKey { get; set; }
    }

    /// <summary>
    /// Type of notification - determines icon and color
    /// </summary>
    public enum NotificationType
    {
        Info = 0,           // bi-info-circle, blue
        Success = 1,        // bi-check-circle, green
        Warning = 2,        // bi-exclamation-triangle, yellow
        Alert = 3,          // bi-exclamation-circle, red
        Mortgage = 4,       // bi-bank, purple
        Document = 5,       // bi-file-earmark-text, gray
        Closing = 6,        // bi-calendar-check, orange
        Deposit = 7,        // bi-cash-stack, green
        Lawyer = 8,         // bi-briefcase, blue
        Purchaser = 9,      // bi-person, teal
        System = 10,        // bi-gear, gray
        Penalty = 11        // bi-exclamation-triangle-fill, red
    }

    /// <summary>
    /// Priority level for notifications
    /// </summary>
    public enum NotificationPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Urgent = 3
    }

    #endregion

    #region Project Financials (Builder-Only, spec Section E)

    /// <summary>
    /// Builder-only investment and profit data. Only builder role can read/write.
    /// Required for AI discount and VTB allocation calculations (spec AI Steps 4-5).
    /// </summary>
    public class ProjectFinancials
    {
        [Key]
        public int Id { get; set; }

        public int ProjectId { get; set; }

        [ForeignKey("ProjectId")]
        public virtual Project Project { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalRevenue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalInvestment { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MarketingCost { get; set; }

        // ProfitAvailable = TotalRevenue - TotalInvestment - MarketingCost
        // Stored explicitly so builder can override if needed
        [Column(TypeName = "decimal(18,2)")]
        public decimal ProfitAvailable { get; set; }

        // Max capital builder can deploy for VTB mortgages (spec AI Step 4)
        [Column(TypeName = "decimal(18,2)")]
        public decimal MaxBuilderCapital { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public string? UpdatedByUserId { get; set; }
    }

    #endregion

    #region Closing Extension / Reschedule Request (spec Sections A.2, C)

    /// <summary>
    /// Purchaser-submitted request to extend or reschedule the closing date.
    /// Builder approves or rejects; SOA is recalculated on approval.
    /// </summary>
    public class ClosingExtensionRequest
    {
        [Key]
        public int Id { get; set; }

        public int UnitId { get; set; }

        [ForeignKey("UnitId")]
        public virtual Unit Unit { get; set; } = null!;

        [Required]
        public string RequestedByPurchaserId { get; set; } = string.Empty;

        [ForeignKey("RequestedByPurchaserId")]
        public virtual ApplicationUser RequestedByPurchaser { get; set; } = null!;

        public DateTime RequestedDate { get; set; } = DateTime.UtcNow;

        // Snapshot of closing date at time of request (for history, spec Section C)
        public DateTime? OriginalClosingDate { get; set; }

        public DateTime RequestedNewClosingDate { get; set; }

        [StringLength(1000)]
        public string? Reason { get; set; }

        public ClosingExtensionStatus Status { get; set; } = ClosingExtensionStatus.Pending;

        public string? ReviewedByBuilderId { get; set; }

        [ForeignKey("ReviewedByBuilderId")]
        public virtual ApplicationUser? ReviewedByBuilder { get; set; }

        public DateTime? ReviewedAt { get; set; }

        [StringLength(500)]
        public string? ReviewerNotes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum ClosingExtensionStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2
    }

    #endregion

    #region System Fee Configuration

    /// <summary>Admin-editable flat fee schedule for Ontario closing costs.
    /// Stores province-wide flat fees (HCRA, Electronic Registration, Status Certificate, Transaction Levy).
    /// Admin can update amounts without a code deploy when Ontario changes the rates.</summary>
    public class SystemFeeConfig
    {
        [Key] public int Id { get; set; }
        /// <summary>Unique key: "HCRA", "ElectronicReg", "StatusCert", "TransactionLevy"</summary>
        [Required][StringLength(50)] public string Key { get; set; } = "";
        [Required][StringLength(100)] public string DisplayName { get; set; } = "";
        [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
        /// <summary>If true, 13% HST is added on top of Amount at closing.</summary>
        public bool HSTApplicable { get; set; } = false;
        /// <summary>If true, Amount already includes all taxes — no additional HST charged.</summary>
        public bool HSTIncluded { get; set; } = false;
        [StringLength(500)] public string? Notes { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? UpdatedByUserId { get; set; }
    }

    #endregion

    #region SOA Version History

    /// <summary>Source of an SOA version snapshot.</summary>
    public enum SOAVersionSource
    {
        SystemCalculation = 0,
        LawyerUpload = 1,
        BuilderUpload = 2,
        LawyerSOAConfirmation = 3
    }

    /// <summary>
    /// Versioned snapshot of a Statement of Adjustments for audit trail and comparison.
    /// A new SOAVersion is created each time the SOA is recalculated or a lawyer uploads a new SOA.
    /// </summary>
    public class SOAVersion
    {
        [Key]
        public int Id { get; set; }

        public int UnitId { get; set; }

        [ForeignKey("UnitId")]
        public virtual Unit Unit { get; set; } = null!;

        /// <summary>Auto-incremented version number per unit (1, 2, 3, ...)</summary>
        public int VersionNumber { get; set; }

        public SOAVersionSource Source { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceDueOnClosing { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalVendorCredits { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPurchaserCredits { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CashRequiredToClose { get; set; }

        /// <summary>File path for lawyer/builder uploaded SOA documents.</summary>
        [StringLength(500)]
        public string? UploadedFilePath { get; set; }

        [Required]
        public string CreatedByUserId { get; set; } = string.Empty;

        [ForeignKey("CreatedByUserId")]
        public virtual ApplicationUser CreatedByUser { get; set; } = null!;

        /// <summary>Role of the user who created this version (Admin, Builder, Lawyer).</summary>
        [Required]
        [StringLength(50)]
        public string CreatedByRole { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    #endregion

    #region Late Closing Penalty

    /// <summary>
    /// Immutable daily accrual record for late closing penalties.
    /// One record per unit per day — no edit/delete endpoints.
    /// </summary>
    public class ClosingPenalty
    {
        [Key]
        public int Id { get; set; }

        public int UnitId { get; set; }

        [ForeignKey("UnitId")]
        public virtual Unit Unit { get; set; } = null!;

        /// <summary>The date this penalty was charged for.</summary>
        public DateTime PenaltyDate { get; set; }

        /// <summary>Daily penalty amount at the time of accrual.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal DailyAmount { get; set; }

        /// <summary>Running total after this accrual.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal AccumulatedTotal { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    #endregion

}
