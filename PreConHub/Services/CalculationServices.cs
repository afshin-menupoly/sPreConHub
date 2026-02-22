using PreConHub.Models.Entities;
using PreConHub.Data;
using Microsoft.EntityFrameworkCore;
using PreConHub.Models.ViewModels;

namespace PreConHub.Services
{
    #region Interfaces

    public interface ISoaCalculationService
    {
        Task<StatementOfAdjustments> CalculateSOAAsync(int unitId, string? createdByUserId = null, string? createdByRole = null);
        Task<StatementOfAdjustments> RecalculateSOAAsync(int unitId);
        Task<bool> LockSOAAsync(int unitId, string userId);
        Task<bool> UnlockSOAAsync(int unitId, string userId, string reason);
        decimal CalculateLandTransferTax(decimal purchasePrice, bool isFirstTimeBuyer = false);
        decimal CalculateTorontoLandTransferTax(decimal purchasePrice, bool isFirstTimeBuyer = false);
    }

    public interface IShortfallAnalysisService
    {
        Task<ShortfallAnalysis> AnalyzeShortfallAsync(int unitId);
        Task<ShortfallAnalysis> RecalculateShortfallAsync(int unitId);
        ClosingRecommendation DetermineRecommendation(decimal shortfallPercentage, bool hasMortgage, decimal? appraisalGap, int? creditScore = null);
        string GenerateRecommendationReasoning(ShortfallAnalysis analysis, Unit unit);
    }

    public interface IProjectSummaryService
    {
        Task<ProjectSummary> CalculateProjectSummaryAsync(int projectId);
        Task RefreshAllProjectSummariesAsync();
    }

    #endregion

    #region SOA Calculation Service

    public class SoaCalculationService : ISoaCalculationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SoaCalculationService> _logger;

        public SoaCalculationService(ApplicationDbContext context, ILogger<SoaCalculationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<StatementOfAdjustments> CalculateSOAAsync(int unitId, string? createdByUserId = null, string? createdByRole = null)
        {
            var unit = await _context.Units
                .Include(u => u.Project)
                    .ThenInclude(p => p.Fees)
                .Include(u => u.Project)
                    .ThenInclude(p => p.LevyCaps)  // NEW: Include levy caps
                .Include(u => u.Deposits)
                    .ThenInclude(d => d.InterestPeriods)
                .Include(u => u.Fees)
                .Include(u => u.OccupancyFees)
                .Include(u => u.Purchasers)
                    .ThenInclude(p => p.MortgageInfo)
                .FirstOrDefaultAsync(u => u.Id == unitId);

            if (unit == null)
                throw new ArgumentException($"Unit with ID {unitId} not found");

            // Load admin-editable system fee configuration
            var systemFees = await _context.SystemFeeConfigs.ToListAsync();

            // Check if SOA is locked - prevent recalculation
            var existingSoa = await _context.StatementsOfAdjustments
                .FirstOrDefaultAsync(s => s.UnitId == unitId);

            if (existingSoa?.IsLocked == true)
            {
                _logger.LogWarning("Attempted to recalculate locked SOA for Unit {UnitId}", unitId);
                throw new InvalidOperationException("SOA is locked and cannot be recalculated. Unlock required.");
            }

            var soa = new StatementOfAdjustments
            {
                UnitId = unitId,
                CalculatedAt = DateTime.UtcNow,
                CalculationVersion = (existingSoa?.CalculationVersion ?? 0) + 1
            };

            // =====================================
            // DEBITS (Amounts purchaser owes)
            // =====================================

            // 1. Purchase Price
            soa.PurchasePrice = unit.PurchasePrice;

            // Total price includes parking and locker for tax/fee calculations
            var parkingAmount = unit.HasParking ? unit.ParkingPrice : 0;
            var lockerAmount = unit.HasLocker ? unit.LockerPrice : 0;
            var totalPrice = unit.PurchasePrice + parkingAmount + lockerAmount;

            // 2. Land Transfer Tax (Ontario) - with first-time buyer check
            soa.LandTransferTax = CalculateLandTransferTax(totalPrice, unit.IsFirstTimeBuyer);

            // 3. Toronto Land Transfer Tax (if applicable)
            bool isInToronto = IsTorontoProperty(unit.Project.City);
            soa.TorontoLandTransferTax = isInToronto
                ? CalculateTorontoLandTransferTax(totalPrice, unit.IsFirstTimeBuyer)
                : 0;

            // 4. Development Charges with Levy Cap Logic
            var actualDevCharges = unit.Project.Fees
                .Where(f => f.FeeType == FeeType.DevelopmentCharges)
                .Sum(f => f.Amount);

            var devChargeCap = unit.Project.LevyCaps
                .FirstOrDefault(c => c.LevyName.Contains("Development"));

            if (devChargeCap != null && actualDevCharges > devChargeCap.CapAmount)
            {
                soa.DevelopmentCharges = devChargeCap.CapAmount;
                if (devChargeCap.ExcessResponsibility == ExcessLevyResponsibility.Builder)
                {
                    soa.BuilderAbsorbedLevies += (actualDevCharges - devChargeCap.CapAmount);
                }
                else
                {
                    soa.DevelopmentCharges = actualDevCharges; // Buyer pays all
                }
            }
            else
            {
                soa.DevelopmentCharges = actualDevCharges;
            }

            // 5. Education Development Charges (EDCs)
            soa.EducationDevelopmentCharges = unit.Project.Fees
                .Where(f => f.FeeType == FeeType.EducationDevelopmentCharges)
                .Sum(f => f.Amount);

            // 6. Parkland Levy
            soa.ParklandLevy = unit.Project.Fees
                .Where(f => f.FeeType == FeeType.ParklandLevy)
                .Sum(f => f.Amount);

            // 7. Community Benefit Charges
            soa.CommunityBenefitCharges = unit.Project.Fees
                .Where(f => f.FeeType == FeeType.CommunityBenefitCharges)
                .Sum(f => f.Amount);

            // 8. Tarion Warranty Fee
            soa.TarionFee = CalculateTarionFee(totalPrice);

            // 9. Utility Connection Fees (combined)
            soa.UtilityConnectionFees = unit.Project.Fees
                .Where(f => f.FeeType == FeeType.UtilityConnection
                         || f.FeeType == FeeType.SewerConnectionFee
                         || f.FeeType == FeeType.WaterConnectionFee
                         || f.FeeType == FeeType.HydroConnectionFee
                         || f.FeeType == FeeType.GasConnectionFee
                         || f.FeeType == FeeType.MeterInstallationFee)
                .Sum(f => f.Amount);

            // 10. Property Tax Adjustment (prorated)
            soa.PropertyTaxAdjustment = CalculatePropertyTaxAdjustment(unit);

            // 11. Common Expense Adjustment (prorated)
            soa.CommonExpenseAdjustment = CalculateCommonExpenseAdjustment(unit);

            // 12. Occupancy Fees (split: chargeable vs paid)
            soa.OccupancyFeesChargeable = unit.OccupancyFees.Sum(o => o.TotalMonthlyFee);
            soa.OccupancyFeesPaid = unit.OccupancyFees.Where(o => o.IsPaid).Sum(o => o.TotalMonthlyFee);
            soa.OccupancyFeesOwing = soa.OccupancyFeesChargeable - soa.OccupancyFeesPaid;

            // 13. Parking
            soa.ParkingPrice = unit.HasParking ? unit.ParkingPrice : 0;

            // 14. Locker
            soa.LockerPrice = unit.HasLocker ? unit.LockerPrice : 0;

            // 15. Upgrades (unit-specific fees that are not credits)
            soa.Upgrades = unit.Fees
                .Where(f => !f.IsCredit)
                .Sum(f => f.Amount);

            // 16. Legal Fees Estimate
            var legalFees = unit.Project.Fees
                .Where(f => f.FeeType == FeeType.LegalFees)
                .Sum(f => f.Amount);
            soa.LegalFeesEstimate = legalFees > 0 ? legalFees : EstimateLegalFees(totalPrice);

            // 17. HST Calculation (NEW - CRITICAL)
            var primaryPurchaser = unit.Purchasers.FirstOrDefault(p => p.IsPrimaryPurchaser);
            bool isPrimaryResidence = unit.IsPrimaryResidence;
            bool isRebateAssigned = true;   // Default for pre-construction

            var hstCalc = CalculateHSTAndRebates(totalPrice, isPrimaryResidence, isRebateAssigned);
            soa.HSTAmount = hstCalc.hst;
            soa.IsHSTRebateEligible = isPrimaryResidence;
            soa.HSTRebateFederal = hstCalc.federalRebate;
            soa.HSTRebateOntario = hstCalc.ontarioRebate;
            soa.HSTRebateTotal = hstCalc.federalRebate + hstCalc.ontarioRebate;
            soa.IsHSTRebateAssignedToBuilder = isRebateAssigned;
            soa.NetHSTPayable = hstCalc.netPayable;

            // 18. Other Debits
            var otherProjectFees = unit.Project.Fees
                .Where(f => f.FeeType == FeeType.Other && f.AppliesToAllUnits)
                .Sum(f => f.Amount);
            soa.OtherDebits = otherProjectFees;

            // 19. System Fees (loaded from SystemFeeConfig, with HST applied)
            soa.HCRAFee = GetSystemFeeWithHST(systemFees, "HCRA");
            soa.ElectronicRegFee = GetSystemFeeWithHST(systemFees, "ElectronicReg");
            soa.StatusCertFee = GetSystemFeeWithHST(systemFees, "StatusCert");
            soa.TransactionLevyFee = GetSystemFeeWithHST(systemFees, "TransactionLevy");

            // Calculate Total Vendor Credits (Credit Vendor — amounts owed TO vendor)
            // NOTE: LTT is informational only — excluded from balance
            soa.TotalVendorCredits = soa.PurchasePrice
                + soa.DevelopmentCharges
                + soa.EducationDevelopmentCharges
                + soa.ParklandLevy
                + soa.CommunityBenefitCharges
                + soa.TarionFee
                + soa.UtilityConnectionFees
                + soa.PropertyTaxAdjustment
                + soa.CommonExpenseAdjustment
                + soa.OccupancyFeesChargeable
                + soa.ParkingPrice
                + soa.LockerPrice
                + soa.Upgrades
                + soa.LegalFeesEstimate
                + soa.NetHSTPayable
                + soa.HCRAFee
                + soa.ElectronicRegFee
                + soa.StatusCertFee
                + soa.TransactionLevyFee
                + soa.OtherDebits;

            soa.TotalDebits = soa.TotalVendorCredits; // backward compat

            // =====================================
            // CREDITS (Amounts reducing obligation)
            // =====================================

            // 1. Deposits Paid
            soa.DepositsPaid = unit.Deposits
                .Where(d => d.IsPaid)
                .Sum(d => d.Amount);

            // 2. Deposit Interest (per-period daily simple interest)
            soa.DepositInterest = CalculatePerPeriodDepositInterest(unit);

            // 3. Interest on Deposit Interest (OccupancyDate to ClosingDate)
            soa.InterestOnDepositInterest = CalculateInterestOnDepositInterest(unit, soa.DepositInterest);

            // 3. Builder Credits (unit-specific credits)
            soa.BuilderCredits = unit.Fees
                .Where(f => f.IsCredit)
                .Sum(f => f.Amount);

            // 4. Design Credits
            soa.DesignCredits = unit.Fees
                .Where(f => f.IsCredit && f.FeeName.Contains("Design", StringComparison.OrdinalIgnoreCase))
                .Sum(f => f.Amount);

            // 5. Free Upgrades Value
            soa.FreeUpgradesValue = unit.Fees
                .Where(f => f.IsCredit && f.FeeName.Contains("Upgrade", StringComparison.OrdinalIgnoreCase))
                .Sum(f => f.Amount);

            // 6. Cash Back Incentives
            soa.CashBackIncentives = unit.Fees
                .Where(f => f.IsCredit && f.FeeName.Contains("Cash", StringComparison.OrdinalIgnoreCase))
                .Sum(f => f.Amount);

            // 7. Other Credits
            soa.OtherCredits = unit.Fees
                .Where(f => f.IsCredit
                    && !f.FeeName.Contains("Design", StringComparison.OrdinalIgnoreCase)
                    && !f.FeeName.Contains("Upgrade", StringComparison.OrdinalIgnoreCase)
                    && !f.FeeName.Contains("Cash", StringComparison.OrdinalIgnoreCase))
                .Sum(f => f.Amount);

            // Calculate Total Purchaser Credits (Credit Purchaser — amounts owed TO purchaser)
            soa.TotalPurchaserCredits = soa.DepositsPaid
                + soa.DepositInterest
                + soa.InterestOnDepositInterest
                + soa.OccupancyFeesPaid
                + soa.SecurityDepositRefund
                + soa.BuilderCredits
                + soa.OtherCredits;

            soa.TotalCredits = soa.TotalPurchaserCredits; // backward compat

            // =====================================
            // FINAL CALCULATIONS
            // =====================================

            // Balance Due on Closing (two-column: Vendor - Purchaser)
            soa.BalanceDueOnClosing = soa.TotalVendorCredits - soa.TotalPurchaserCredits;

            // Mortgage Amount (from primary purchaser)
            soa.MortgageAmount = primaryPurchaser?.MortgageInfo?.ApprovedAmount ?? 0;

            // Cash Required to Close
            soa.CashRequiredToClose = soa.BalanceDueOnClosing - soa.MortgageAmount;

            // Save or update
            if (existingSoa != null)
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    EntityType = "StatementOfAdjustments",
                    EntityId = unitId,
                    Action = "Recalculate",
                    OldValues = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        existingSoa.BalanceDueOnClosing,
                        existingSoa.TotalDebits,
                        existingSoa.TotalCredits,
                        existingSoa.CalculationVersion,
                        existingSoa.CalculatedAt
                    }),
                    Timestamp = DateTime.UtcNow
                });
                soa.Id = existingSoa.Id;
                _context.Entry(existingSoa).CurrentValues.SetValues(soa);
                existingSoa.RecalculatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.StatementsOfAdjustments.Add(soa);
            }

            await _context.SaveChangesAsync();

            // Create SOAVersion snapshot for audit trail (only if user context available)
            if (!string.IsNullOrEmpty(createdByUserId))
            {
                var lastVersion = await _context.SOAVersions
                    .Where(v => v.UnitId == unitId)
                    .OrderByDescending(v => v.VersionNumber)
                    .Select(v => v.VersionNumber)
                    .FirstOrDefaultAsync();

                _context.SOAVersions.Add(new SOAVersion
                {
                    UnitId = unitId,
                    VersionNumber = lastVersion + 1,
                    Source = SOAVersionSource.SystemCalculation,
                    BalanceDueOnClosing = soa.BalanceDueOnClosing,
                    TotalVendorCredits = soa.TotalVendorCredits,
                    TotalPurchaserCredits = soa.TotalPurchaserCredits,
                    CashRequiredToClose = soa.CashRequiredToClose,
                    CreatedByUserId = createdByUserId,
                    CreatedByRole = createdByRole ?? "System",
                    CreatedAt = DateTime.UtcNow,
                    Notes = $"Auto-calculated. Balance: {soa.BalanceDueOnClosing:C2}"
                });
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("SOA calculated for Unit {UnitId}. Balance Due: {Balance}, HST: {HST}",
                unitId, soa.BalanceDueOnClosing, soa.NetHSTPayable);

            return soa;
        }

        /// <summary>
        /// Per-period daily simple interest: Amount × (Rate/100) × (DaysInPeriod/365).
        /// Uses DepositInterestPeriod rows if available; falls back to deposit-level InterestRate.
        /// </summary>
        private decimal CalculatePerPeriodDepositInterest(Unit unit)
        {
            decimal totalInterest = 0;
            var closingDate = unit.ClosingDate ?? DateTime.UtcNow;

            foreach (var deposit in unit.Deposits.Where(d => d.IsPaid && d.PaidDate.HasValue))
            {
                var depositDate = deposit.PaidDate!.Value;

                if (deposit.InterestPeriods.Any())
                {
                    // Per-period daily simple interest from DepositInterestPeriod rows
                    foreach (var period in deposit.InterestPeriods.OrderBy(p => p.PeriodStart))
                    {
                        var effectiveStart = depositDate > period.PeriodStart ? depositDate : period.PeriodStart;
                        var effectiveEnd = closingDate < period.PeriodEnd ? closingDate : period.PeriodEnd;

                        if (effectiveEnd > effectiveStart)
                        {
                            var daysInPeriod = (effectiveEnd - effectiveStart).Days;
                            totalInterest += deposit.Amount * (period.AnnualRate / 100m) * (daysInPeriod / 365m);
                        }
                    }
                }
                else if (deposit.IsInterestEligible && deposit.InterestRate.HasValue)
                {
                    // Fallback: use deposit-level rate with simple interest
                    var daysHeld = (closingDate - depositDate).Days;
                    totalInterest += deposit.Amount * deposit.InterestRate.Value * (daysHeld / 365m);
                }
            }

            return Math.Round(totalInterest, 2);
        }

        /// <summary>
        /// Interest on the total deposit interest amount, from OccupancyDate to ClosingDate
        /// at the last applicable government rate.
        /// </summary>
        private decimal CalculateInterestOnDepositInterest(Unit unit, decimal depositInterest)
        {
            if (depositInterest <= 0) return 0;
            if (!unit.OccupancyDate.HasValue || !unit.ClosingDate.HasValue) return 0;
            if (unit.ClosingDate.Value <= unit.OccupancyDate.Value) return 0;

            // Use the last applicable rate from any deposit's interest periods
            var lastRate = unit.Deposits
                .SelectMany(d => d.InterestPeriods)
                .OrderByDescending(p => p.PeriodEnd)
                .Select(p => p.AnnualRate)
                .FirstOrDefault();

            if (lastRate <= 0) return 0;

            var days = (unit.ClosingDate.Value - unit.OccupancyDate.Value).Days;
            return Math.Round(depositInterest * (lastRate / 100m) * (days / 365m), 2);
        }

        /// <summary>
        /// Look up a SystemFeeConfig by key and return the effective amount with HST applied.
        /// </summary>
        private static decimal GetSystemFeeWithHST(List<SystemFeeConfig> fees, string key)
        {
            var fee = fees.FirstOrDefault(f => f.Key == key);
            if (fee == null) return 0;
            if (fee.HSTApplicable) return Math.Round(fee.Amount * 1.13m, 2);
            return fee.Amount; // HSTIncluded or no HST
        }

        /// <summary>
        /// Lock SOA after builder and lawyer confirmation
        /// </summary>
        public async Task<bool> LockSOAAsync(int unitId, string userId)
        {
            var soa = await _context.StatementsOfAdjustments
                .FirstOrDefaultAsync(s => s.UnitId == unitId);

            if (soa == null) return false;

            // Require both confirmations before locking
            if (!soa.IsConfirmedByBuilder || !soa.IsConfirmedByLawyer)
            {
                _logger.LogWarning("Cannot lock SOA for Unit {UnitId} - missing confirmations", unitId);
                return false;
            }

            soa.IsLocked = true;
            soa.LockedAt = DateTime.UtcNow;
            soa.LockedByUserId = userId;

            await _context.SaveChangesAsync();

            _logger.LogInformation("SOA locked for Unit {UnitId} by User {UserId}", unitId, userId);
            return true;
        }

        /// <summary>
        /// Unlock SOA for re-calculation (requires authorization)
        /// </summary>
        public async Task<bool> UnlockSOAAsync(int unitId, string userId, string reason)
        {
            var soa = await _context.StatementsOfAdjustments
                .FirstOrDefaultAsync(s => s.UnitId == unitId);

            if (soa == null || !soa.IsLocked) return false;

            // Reset lock and confirmations
            soa.IsLocked = false;
            soa.LockedAt = null;
            soa.LockedByUserId = null;
            soa.IsConfirmedByBuilder = false;
            soa.BuilderConfirmedAt = null;
            soa.IsConfirmedByLawyer = false;
            soa.ConfirmedAt = null;

            // Log the unlock event
            _logger.LogWarning("SOA unlocked for Unit {UnitId} by User {UserId}. Reason: {Reason}",
                unitId, userId, reason);

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<StatementOfAdjustments> RecalculateSOAAsync(int unitId)
        {
            return await CalculateSOAAsync(unitId);
        }

        /// <summary>
        /// Ontario Land Transfer Tax calculation (2024 rates)
        /// </summary>
        public decimal CalculateLandTransferTax(decimal purchasePrice, bool isFirstTimeBuyer = false)
        {
            decimal tax = 0;

            if (purchasePrice <= 55000)
            {
                tax = purchasePrice * 0.005m;
            }
            else if (purchasePrice <= 250000)
            {
                tax = (55000 * 0.005m) + ((purchasePrice - 55000) * 0.01m);
            }
            else if (purchasePrice <= 400000)
            {
                tax = (55000 * 0.005m) + (195000 * 0.01m) + ((purchasePrice - 250000) * 0.015m);
            }
            else if (purchasePrice <= 2000000)
            {
                tax = (55000 * 0.005m) + (195000 * 0.01m) + (150000 * 0.015m) + ((purchasePrice - 400000) * 0.02m);
            }
            else
            {
                tax = (55000 * 0.005m) + (195000 * 0.01m) + (150000 * 0.015m) + (1600000 * 0.02m) + ((purchasePrice - 2000000) * 0.025m);
            }

            if (isFirstTimeBuyer)
            {
                decimal rebate = Math.Min(tax, 4000);
                tax -= rebate;
            }

            return Math.Round(tax, 2);
        }

        /// <summary>
        /// Toronto Municipal Land Transfer Tax (2024 rates)
        /// </summary>
        public decimal CalculateTorontoLandTransferTax(decimal purchasePrice, bool isFirstTimeBuyer = false)
        {
            decimal tax = 0;

            if (purchasePrice <= 55000)
            {
                tax = purchasePrice * 0.005m;
            }
            else if (purchasePrice <= 250000)
            {
                tax = (55000 * 0.005m) + ((purchasePrice - 55000) * 0.01m);
            }
            else if (purchasePrice <= 400000)
            {
                tax = (55000 * 0.005m) + (195000 * 0.01m) + ((purchasePrice - 250000) * 0.015m);
            }
            else if (purchasePrice <= 2000000)
            {
                tax = (55000 * 0.005m) + (195000 * 0.01m) + (150000 * 0.015m) + ((purchasePrice - 400000) * 0.02m);
            }
            else
            {
                tax = (55000 * 0.005m) + (195000 * 0.01m) + (150000 * 0.015m) + (1600000 * 0.02m) + ((purchasePrice - 2000000) * 0.025m);
            }

            if (isFirstTimeBuyer)
            {
                decimal rebate = Math.Min(tax, 4475);
                tax -= rebate;
            }

            return Math.Round(tax, 2);
        }

        private bool IsTorontoProperty(string city)
        {
            var torontoCities = new[] { "toronto", "north york", "scarborough", "etobicoke", "york", "east york" };
            return torontoCities.Contains(city?.ToLower() ?? "");
        }

        private decimal CalculateTarionFee(decimal purchasePrice)
        {
            if (purchasePrice <= 100000) return 300;
            if (purchasePrice <= 150000) return 430;
            if (purchasePrice <= 200000) return 515;
            if (purchasePrice <= 250000) return 610;
            if (purchasePrice <= 300000) return 720;
            if (purchasePrice <= 350000) return 835;
            if (purchasePrice <= 400000) return 950;
            if (purchasePrice <= 500000) return 1130;
            if (purchasePrice <= 600000) return 1350;
            if (purchasePrice <= 700000) return 1550;
            if (purchasePrice <= 850000) return 1850;
            if (purchasePrice <= 1000000) return 2150;
            return 2450;
        }

        private decimal CalculatePropertyTaxAdjustment(Unit unit)
        {
            if (unit.ClosingDate == null) return 0;

            // Use actual land tax if builder entered it; otherwise estimate at 1% of purchase price
            decimal annualTax = (unit.ActualAnnualLandTax.HasValue && unit.ActualAnnualLandTax.Value > 0)
                ? unit.ActualAnnualLandTax.Value
                : unit.PurchasePrice * 0.01m;

            int daysInYear = DateTime.IsLeapYear(unit.ClosingDate.Value.Year) ? 366 : 365;
            // Purchaser reimburses vendor for remaining days of the year after closing
            int purchaserDays = daysInYear - unit.ClosingDate.Value.DayOfYear;
            return Math.Round(annualTax * purchaserDays / daysInYear, 2);
        }

        private decimal CalculateCommonExpenseAdjustment(Unit unit)
        {
            if (unit.ClosingDate == null) return 0;

            // Use actual maintenance fee if builder entered it; otherwise estimate at $0.60/sqft
            decimal monthlyFee = (unit.ActualMonthlyMaintenanceFee.HasValue && unit.ActualMonthlyMaintenanceFee.Value > 0)
                ? unit.ActualMonthlyMaintenanceFee.Value
                : unit.SquareFootage * 0.60m;

            int daysInMonth = DateTime.DaysInMonth(unit.ClosingDate.Value.Year, unit.ClosingDate.Value.Month);
            // Purchaser reimburses vendor for remaining days of the closing month
            int daysRemaining = daysInMonth - unit.ClosingDate.Value.Day;
            return Math.Round((monthlyFee / daysInMonth) * daysRemaining, 2);
        }



        private decimal EstimateLegalFees(decimal purchasePrice)
        {
            if (purchasePrice <= 500000) return 1500;
            if (purchasePrice <= 1000000) return 2000;
            return 2500;
        }

        /// <summary>
        /// Calculate HST and rebates for new home purchase
        /// Ontario HST = 13% (5% federal + 8% provincial)
        /// </summary>
        private (decimal hst, decimal federalRebate, decimal ontarioRebate, decimal netPayable)
            CalculateHSTAndRebates(decimal purchasePrice, bool isPrimaryResidence, bool isRebateAssignedToBuilder)
        {
            // HST in Ontario is 13%
            decimal hstRate = 0.13m;
            decimal hstAmount = purchasePrice * hstRate;

            decimal federalRebate = 0;
            decimal ontarioRebate = 0;

            if (isPrimaryResidence)
            {
                // Federal New Housing Rebate (36% of 5% GST portion, max $6,300)
                // Full rebate if price <= $350,000
                // Partial rebate if $350,000 < price <= $450,000
                // No rebate if price > $450,000
                decimal gstPortion = purchasePrice * 0.05m;

                if (purchasePrice <= 350000)
                {
                    federalRebate = Math.Min(gstPortion * 0.36m, 6300);
                }
                else if (purchasePrice <= 450000)
                {
                    decimal factor = (450000 - purchasePrice) / 100000;
                    federalRebate = Math.Min(gstPortion * 0.36m * factor, 6300);
                }

                // Ontario New Housing Rebate (75% of 8% PST portion, max $24,000)
                // Full rebate if price <= $400,000
                // No rebate if price > $400,000 (but still available for new construction)
                // For new construction: rebate available regardless of price (capped at $24,000)
                decimal pstPortion = purchasePrice * 0.08m;
                ontarioRebate = Math.Min(pstPortion * 0.75m, 24000);
            }

            decimal totalRebate = federalRebate + ontarioRebate;

            // If rebate is assigned to builder, buyer pays net amount
            // If not assigned, buyer pays full HST and receives rebate later
            decimal netPayable = isRebateAssignedToBuilder
                ? hstAmount - totalRebate
                : hstAmount;

            return (hstAmount, federalRebate, ontarioRebate, netPayable);
        }

    }

    #endregion

    #region Shortfall Analysis Service

    public class ShortfallAnalysisService : IShortfallAnalysisService
    {
        private readonly ApplicationDbContext _context;
        private readonly ISoaCalculationService _soaService;
        private readonly ILogger<ShortfallAnalysisService> _logger;

        public ShortfallAnalysisService(
            ApplicationDbContext context, 
            ISoaCalculationService soaService,
            ILogger<ShortfallAnalysisService> logger)
        {
            _context = context;
            _soaService = soaService;
            _logger = logger;
        }

        public async Task<ShortfallAnalysis> AnalyzeShortfallAsync(int unitId)
        {
            // First ensure SOA is calculated
            var soa = await _context.StatementsOfAdjustments
                .FirstOrDefaultAsync(s => s.UnitId == unitId);

            if (soa == null)
            {
                soa = await _soaService.CalculateSOAAsync(unitId);
            }

            var unit = await _context.Units
                .Include(u => u.Deposits)
                .Include(u => u.Project)
                    .ThenInclude(p => p.Financials)
                .Include(u => u.Purchasers)
                    .ThenInclude(p => p.MortgageInfo)
                .Include(u => u.Purchasers)
                    .ThenInclude(p => p.Financials)
                .FirstOrDefaultAsync(u => u.Id == unitId);

            if (unit == null)
                throw new ArgumentException($"Unit with ID {unitId} not found");

            var primaryPurchaser = unit.Purchasers.FirstOrDefault(p => p.IsPrimaryPurchaser);
            var mortgageInfo = primaryPurchaser?.MortgageInfo;
            var financials = primaryPurchaser?.Financials;

            // FIX: Check for existing analysis and use tracked entity
            var analysis = await _context.ShortfallAnalyses
                .FirstOrDefaultAsync(s => s.UnitId == unitId);

            bool isNew = analysis == null;

            if (isNew)
            {
                analysis = new ShortfallAnalysis
                {
                    UnitId = unitId,
                    CalculatedAt = DateTime.UtcNow
                };
            }

            // Update all properties on the tracked entity
            analysis.SOAAmount = soa.BalanceDueOnClosing;
            analysis.MortgageApproved = mortgageInfo?.ApprovedAmount ?? 0;
            analysis.DepositsPaid = unit.Deposits.Where(d => d.IsPaid).Sum(d => d.Amount);
            // Use TotalFundsAvailable (includes RRSP, Gift, ProceedsFromSale, OtherFunds + AdditionalCash)
            // so all personal funds are considered in the shortfall calculation (spec Step 1).
            analysis.AdditionalCashAvailable = financials?.TotalFundsAvailable ?? financials?.AdditionalCashAvailable ?? 0;

            // Calculate total funds available
            analysis.TotalFundsAvailable = analysis.MortgageApproved
                + analysis.DepositsPaid
                + analysis.AdditionalCashAvailable;

            // Calculate shortfall
            analysis.ShortfallAmount = soa.CashRequiredToClose - analysis.AdditionalCashAvailable;
            
            if (analysis.ShortfallAmount < 0)
                analysis.ShortfallAmount = 0;

            // Shortfall percentage
            analysis.ShortfallPercentage = unit.PurchasePrice > 0
                ? Math.Round((analysis.ShortfallAmount / unit.PurchasePrice) * 100, 2)
                : 0;

            // Determine appraisal gap
            decimal? appraisalGap = null;
            if (unit.CurrentAppraisalValue.HasValue && unit.CurrentAppraisalValue > 0)
            {
                appraisalGap = unit.PurchasePrice - unit.CurrentAppraisalValue.Value;
            }

            // Step 2 (spec AI): Mutual Release Threshold check
            // Uses purchaser-provided appraisal if available, falls back to builder-set value
            decimal? appraisedValue = mortgageInfo?.PurchaserAppraisalValue ?? unit.CurrentAppraisalValue;
            bool mutualReleaseTriggered = false;

            if (appraisedValue.HasValue && appraisedValue.Value > 0 && appraisedValue.Value < unit.PurchasePrice)
            {
                decimal mutualReleaseThreshold = unit.PurchasePrice - ((unit.PurchasePrice - appraisedValue.Value) / 3m);
                analysis.MutualReleaseThreshold = mutualReleaseThreshold;

                if ((analysis.DepositsPaid + analysis.AdditionalCashAvailable) >= mutualReleaseThreshold)
                    mutualReleaseTriggered = true;
            }

            int? creditScore = mortgageInfo?.CreditScore;

            // Determine risk level
            analysis.RiskLevel = DetermineRiskLevel(analysis.ShortfallPercentage, mortgageInfo?.HasMortgageApproval ?? false);

            // Step 3 (spec AI): Determine recommendation
            // Mutual release (Step 2) takes priority over the tiered logic
            if (mutualReleaseTriggered)
            {
                analysis.Recommendation = ClosingRecommendation.MutualRelease;
            }
            else
            {
                analysis.Recommendation = DetermineRecommendation(
                    analysis.ShortfallPercentage,
                    mortgageInfo?.HasMortgageApproval ?? false,
                    appraisalGap,
                    creditScore);
            }

            // Step 4 (spec AI): Allocate discount and VTB using project financials
            int unitsUnsold = await _context.Units
                .CountAsync(u => u.ProjectId == unit.ProjectId && u.Status != UnitStatus.Closed);
            unitsUnsold = Math.Max(unitsUnsold, 1);

            var projectFinancials = unit.Project?.Financials;

            if (projectFinancials != null)
            {
                decimal profitPerUnit = projectFinancials.ProfitAvailable / unitsUnsold;
                decimal vtbCapPerUnit = projectFinancials.MaxBuilderCapital / unitsUnsold;
                decimal discountAllocated = Math.Min(analysis.ShortfallAmount, profitPerUnit);
                decimal vtbAllocated = Math.Min(analysis.ShortfallAmount - discountAllocated, vtbCapPerUnit);

                switch (analysis.Recommendation)
                {
                    case ClosingRecommendation.CloseWithDiscount:
                        analysis.SuggestedDiscount = discountAllocated;
                        analysis.SuggestedVTBAmount = null;
                        break;
                    case ClosingRecommendation.VTBSecondMortgage:
                        analysis.SuggestedDiscount = discountAllocated > 0 ? discountAllocated : null;
                        analysis.SuggestedVTBAmount = vtbAllocated;
                        break;
                    case ClosingRecommendation.VTBFirstMortgage:
                        // Spec Step 3: VTB First capped at min(75% of APS_Unit, MaxBuilderCapital)
                        decimal vtbFirstCap = Math.Min(unit.PurchasePrice * 0.75m, vtbCapPerUnit);
                        analysis.SuggestedDiscount = discountAllocated > 0 ? discountAllocated : null;
                        analysis.SuggestedVTBAmount = Math.Min(analysis.ShortfallAmount - (discountAllocated > 0 ? discountAllocated : 0), vtbFirstCap);
                        break;
                    case ClosingRecommendation.CombinationSuggestion:
                        analysis.SuggestedDiscount = discountAllocated > 0 ? discountAllocated : null;
                        analysis.SuggestedVTBAmount = vtbAllocated > 0 ? vtbAllocated : null;
                        break;
                    default:
                        analysis.SuggestedDiscount = null;
                        analysis.SuggestedVTBAmount = null;
                        break;
                }
            }
            else
            {
                // Fallback: no project financials configured — use full shortfall
                switch (analysis.Recommendation)
                {
                    case ClosingRecommendation.CloseWithDiscount:
                        analysis.SuggestedDiscount = analysis.ShortfallAmount;
                        analysis.SuggestedVTBAmount = null;
                        break;
                    case ClosingRecommendation.VTBSecondMortgage:
                    case ClosingRecommendation.VTBFirstMortgage:
                        analysis.SuggestedVTBAmount = analysis.ShortfallAmount;
                        analysis.SuggestedDiscount = null;
                        break;
                    default:
                        analysis.SuggestedDiscount = null;
                        analysis.SuggestedVTBAmount = null;
                        break;
                }
            }

            // Generate reasoning
            analysis.RecommendationReasoning = GenerateRecommendationReasoning(analysis, unit);

            // Update metadata
            if (!isNew)
            {
                analysis.RecalculatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.ShortfallAnalyses.Add(analysis);
            }

            // Update unit status and recommendation
            unit.Recommendation = analysis.Recommendation;
            unit.Status = MapRecommendationToStatus(analysis.Recommendation);
            unit.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Shortfall analysis completed for Unit {UnitId}. Shortfall: {Shortfall} ({Percentage}%)", 
                unitId, analysis.ShortfallAmount, analysis.ShortfallPercentage);

            return analysis;
        }

        public async Task<ShortfallAnalysis> RecalculateShortfallAsync(int unitId)
        {
            await _soaService.RecalculateSOAAsync(unitId);
            return await AnalyzeShortfallAsync(unitId);
        }

        private RiskLevel DetermineRiskLevel(decimal shortfallPercentage, bool hasMortgage)
        {
            if (!hasMortgage)
                return RiskLevel.VeryHigh;

            if (shortfallPercentage <= 5)
                return RiskLevel.Low;
            if (shortfallPercentage <= 15)
                return RiskLevel.Medium;
            if (shortfallPercentage <= 25)
                return RiskLevel.High;
            
            return RiskLevel.VeryHigh;
        }

        public ClosingRecommendation DetermineRecommendation(decimal shortfallPercentage, bool hasMortgage, decimal? appraisalGap, int? creditScore = null)
        {
            if (shortfallPercentage <= 0)
                return ClosingRecommendation.ProceedToClose;

            if (shortfallPercentage <= 10)
                return ClosingRecommendation.CloseWithDiscount;

            if (shortfallPercentage <= 20)
                return ClosingRecommendation.VTBSecondMortgage;

            // VTB 1st mortgage: 20-50% shortfall with credit score >= 700 (spec AI Step 3)
            if (shortfallPercentage <= 50 && (creditScore ?? 0) >= 700)
                return ClosingRecommendation.VTBFirstMortgage;

            // Default: > 50% shortfall with credit score < 600 (spec AI Step 3)
            if (shortfallPercentage > 50 && (creditScore ?? int.MaxValue) < 600)
                return ClosingRecommendation.HighRiskDefault;

            // All other cases: AI suggests combination of discount + VTB + extension
            return ClosingRecommendation.CombinationSuggestion;
        }

        public string GenerateRecommendationReasoning(ShortfallAnalysis analysis, Unit unit)
        {
            var reasons = new List<string>();

            switch (analysis.Recommendation)
            {
                case ClosingRecommendation.ProceedToClose:
                    reasons.Add($"Purchaser has sufficient funds to close. No shortfall detected.");
                    reasons.Add($"Mortgage approved: ${analysis.MortgageApproved:N0}");
                    reasons.Add($"Additional cash available: ${analysis.AdditionalCashAvailable:N0}");
                    break;

                case ClosingRecommendation.CloseWithDiscount:
                    reasons.Add($"Shortfall of ${analysis.ShortfallAmount:N0} ({analysis.ShortfallPercentage}% of purchase price).");
                    reasons.Add($"Recommend offering a discount or credit of ${analysis.SuggestedDiscount:N0} to enable closing.");
                    reasons.Add("Purchaser has mortgage approval and can close with minor assistance.");
                    break;

                case ClosingRecommendation.VTBSecondMortgage:
                    reasons.Add($"Moderate shortfall of ${analysis.ShortfallAmount:N0} ({analysis.ShortfallPercentage}% of purchase price).");
                    reasons.Add($"Recommend Vendor Take-Back (VTB) second mortgage of ${analysis.SuggestedVTBAmount:N0}.");
                    reasons.Add("Primary mortgage is in place. VTB would be subordinate to first mortgage.");
                    break;

                case ClosingRecommendation.VTBFirstMortgage:
                    reasons.Add($"Significant shortfall of ${analysis.ShortfallAmount:N0} ({analysis.ShortfallPercentage}% of purchase price).");
                    if (analysis.MortgageApproved == 0)
                    {
                        reasons.Add("Purchaser has no mortgage approval.");
                        reasons.Add($"Recommend VTB first mortgage up to 75% of APS (${unit.PurchasePrice * 0.75m:N0} max).");
                    }
                    else
                    {
                        reasons.Add($"Recommend restructuring with VTB first mortgage of ${analysis.SuggestedVTBAmount:N0}.");
                    }
                    reasons.Add("High risk scenario - recommend additional due diligence on purchaser ability to service debt.");
                    break;

                case ClosingRecommendation.HighRiskDefault:
                case ClosingRecommendation.PotentialDefault:
                    reasons.Add($"Critical shortfall of ${analysis.ShortfallAmount:N0} ({analysis.ShortfallPercentage}% of purchase price).");
                    reasons.Add("Risk assessment indicates high probability of default.");
                    reasons.Add("Options: 1) Negotiate assignment sale, 2) Mutual release, 3) Default proceedings.");
                    reasons.Add("Recommend legal review before proceeding.");
                    break;

                case ClosingRecommendation.MutualRelease:
                    reasons.Add("Purchaser deposits and personal funds meet the mutual release threshold.");
                    reasons.Add($"Mutual release threshold: ${analysis.MutualReleaseThreshold:N0}.");
                    reasons.Add("Purchaser can exit the contract without additional financial loss. No discount or VTB required.");
                    break;

                case ClosingRecommendation.CombinationSuggestion:
                    reasons.Add($"Shortfall of ${analysis.ShortfallAmount:N0} ({analysis.ShortfallPercentage}% of purchase price).");
                    reasons.Add("AI suggests a combination approach: partial discount + VTB second mortgage + optional closing extension.");
                    if (analysis.SuggestedDiscount.HasValue)
                        reasons.Add($"Suggested discount component: ${analysis.SuggestedDiscount:N0}.");
                    if (analysis.SuggestedVTBAmount.HasValue)
                        reasons.Add($"Suggested VTB component: ${analysis.SuggestedVTBAmount:N0}.");
                    break;
            }

            if (unit.CurrentAppraisalValue.HasValue && unit.CurrentAppraisalValue < unit.PurchasePrice)
            {
                decimal gap = unit.PurchasePrice - unit.CurrentAppraisalValue.Value;
                reasons.Add($"Note: Current appraisal (${unit.CurrentAppraisalValue:N0}) is ${gap:N0} below purchase price.");
            }

            return string.Join("\n", reasons);
        }

        private UnitStatus MapRecommendationToStatus(ClosingRecommendation recommendation)
        {
            return recommendation switch
            {
                ClosingRecommendation.ProceedToClose        => UnitStatus.ReadyToClose,
                ClosingRecommendation.CloseWithDiscount     => UnitStatus.NeedsDiscount,
                ClosingRecommendation.VTBSecondMortgage     => UnitStatus.NeedsVTB,
                ClosingRecommendation.VTBFirstMortgage      => UnitStatus.NeedsVTB,
                ClosingRecommendation.HighRiskDefault       => UnitStatus.AtRisk,
                ClosingRecommendation.PotentialDefault      => UnitStatus.AtRisk,
                ClosingRecommendation.MutualRelease         => UnitStatus.AtRisk,
                ClosingRecommendation.CombinationSuggestion => UnitStatus.NeedsVTB,
                _ => UnitStatus.UnderReview
            };
        }
    }

    #endregion

    #region Project Summary Service

    public class ProjectSummaryService : IProjectSummaryService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProjectSummaryService> _logger;

        public ProjectSummaryService(ApplicationDbContext context, ILogger<ProjectSummaryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ProjectSummary> CalculateProjectSummaryAsync(int projectId)
        {
            var project = await _context.Projects
                .Include(p => p.Units)
                    .ThenInclude(u => u.ShortfallAnalysis)
                .Include(p => p.Financials)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                throw new ArgumentException($"Project with ID {projectId} not found");

            var units = project.Units.ToList();
            var totalUnits = units.Count;

            if (totalUnits == 0)
            {
                return new ProjectSummary { ProjectId = projectId, TotalUnits = 0 };
            }

            // Spec Step 4: Proportional scaling of discount/VTB if aggregate exceeds budget
            var financials = project.Financials;
            if (financials != null)
            {
                var unitsWithAnalysis = units.Where(u => u.ShortfallAnalysis != null && u.Status != UnitStatus.Closed).ToList();

                decimal totalDiscountAllocated = unitsWithAnalysis.Sum(u => u.ShortfallAnalysis!.SuggestedDiscount ?? 0);
                decimal totalVTBAllocated = unitsWithAnalysis.Sum(u => u.ShortfallAnalysis!.SuggestedVTBAmount ?? 0);
                bool changed = false;

                // Scale discounts proportionally if they exceed ProfitAvailable
                if (totalDiscountAllocated > financials.ProfitAvailable && totalDiscountAllocated > 0)
                {
                    decimal scaleFactor = financials.ProfitAvailable / totalDiscountAllocated;
                    foreach (var u in unitsWithAnalysis.Where(u => u.ShortfallAnalysis!.SuggestedDiscount > 0))
                    {
                        u.ShortfallAnalysis!.SuggestedDiscount = Math.Round(u.ShortfallAnalysis.SuggestedDiscount!.Value * scaleFactor, 2);
                    }
                    changed = true;
                }

                // Scale VTB proportionally if it exceeds MaxBuilderCapital
                if (totalVTBAllocated > financials.MaxBuilderCapital && totalVTBAllocated > 0)
                {
                    decimal scaleFactor = financials.MaxBuilderCapital / totalVTBAllocated;
                    foreach (var u in unitsWithAnalysis.Where(u => u.ShortfallAnalysis!.SuggestedVTBAmount > 0))
                    {
                        u.ShortfallAnalysis!.SuggestedVTBAmount = Math.Round(u.ShortfallAnalysis.SuggestedVTBAmount!.Value * scaleFactor, 2);
                    }
                    changed = true;
                }

                if (changed)
                {
                    await _context.SaveChangesAsync();
                }
            }

            // Count by status
            int readyToClose = units.Count(u => u.Recommendation == ClosingRecommendation.ProceedToClose);
            int needingDiscount = units.Count(u => u.Recommendation == ClosingRecommendation.CloseWithDiscount);
            int needingVTB = units.Count(u => u.Recommendation == ClosingRecommendation.VTBSecondMortgage 
                                            || u.Recommendation == ClosingRecommendation.VTBFirstMortgage);
            int atRisk = units.Count(u => u.Recommendation == ClosingRecommendation.HighRiskDefault
                                        || u.Recommendation == ClosingRecommendation.PotentialDefault);
            int pendingData = units.Count(u => u.Status == UnitStatus.Pending);

            // Financial calculations
            decimal totalSalesValue = units.Sum(u => u.PurchasePrice);
            
            decimal totalDiscountRequired = units
                .Where(u => u.ShortfallAnalysis != null && u.Recommendation == ClosingRecommendation.CloseWithDiscount)
                .Sum(u => u.ShortfallAnalysis!.SuggestedDiscount ?? 0);

            decimal totalInvestmentAtRisk = units
                .Where(u => u.Recommendation == ClosingRecommendation.HighRiskDefault 
                         || u.Recommendation == ClosingRecommendation.PotentialDefault)
                .Sum(u => u.PurchasePrice);

            decimal totalShortfall = units
                .Where(u => u.ShortfallAnalysis != null)
                .Sum(u => u.ShortfallAnalysis!.ShortfallAmount);

            // Step 5 (spec AI): Total fund still needed after discount + VTB allocation
            decimal totalFundNeededToClose = units
                .Where(u => u.ShortfallAnalysis != null && u.Status != UnitStatus.Closed)
                .Sum(u => Math.Max(0,
                    u.ShortfallAnalysis!.ShortfallAmount
                    - (u.ShortfallAnalysis.SuggestedDiscount ?? 0)
                    - (u.ShortfallAnalysis.SuggestedVTBAmount ?? 0)));

            // FIX: Check for existing and update directly
            var summary = await _context.ProjectSummaries
                .FirstOrDefaultAsync(ps => ps.ProjectId == projectId);

            bool isNew = summary == null;

            if (isNew)
            {
                summary = new ProjectSummary { ProjectId = projectId };
            }

            // Update all properties
            summary.TotalUnits = totalUnits;
            summary.UnitsReadyToClose = readyToClose;
            summary.UnitsNeedingDiscount = needingDiscount;
            summary.UnitsNeedingVTB = needingVTB;
            summary.UnitsAtRisk = atRisk;
            summary.UnitsPendingData = pendingData;
            summary.PercentReadyToClose = Math.Round((decimal)readyToClose / totalUnits * 100, 2);
            summary.PercentNeedingDiscount = Math.Round((decimal)needingDiscount / totalUnits * 100, 2);
            summary.PercentNeedingVTB = Math.Round((decimal)needingVTB / totalUnits * 100, 2);
            summary.PercentAtRisk = Math.Round((decimal)atRisk / totalUnits * 100, 2);
            summary.TotalSalesValue = totalSalesValue;
            summary.TotalDiscountRequired = totalDiscountRequired;
            summary.DiscountPercentOfSales = totalSalesValue > 0 
                ? Math.Round(totalDiscountRequired / totalSalesValue * 100, 2) 
                : 0;
            summary.TotalInvestmentAtRisk = totalInvestmentAtRisk;
            summary.TotalShortfall = totalShortfall;
            summary.TotalFundNeededToClose = totalFundNeededToClose;
            summary.ClosingProbabilityPercent = Math.Round((decimal)(readyToClose + needingDiscount + needingVTB) / totalUnits * 100, 2);
            summary.CalculatedAt = DateTime.UtcNow;

            if (isNew)
            {
                _context.ProjectSummaries.Add(summary);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Project summary calculated for Project {ProjectId}. " +
                "Ready: {Ready}, Discount: {Discount}, VTB: {VTB}, Risk: {Risk}", 
                projectId, readyToClose, needingDiscount, needingVTB, atRisk);

            return summary;
        }

        public async Task RefreshAllProjectSummariesAsync()
        {
            var projectIds = await _context.Projects
                .Where(p => p.Status == ProjectStatus.Active)
                .Select(p => p.Id)
                .ToListAsync();

            foreach (var projectId in projectIds)
            {
                await CalculateProjectSummaryAsync(projectId);
            }
        }


    }

    #endregion

}
