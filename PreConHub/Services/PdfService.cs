// ============================================================
// PDF SERVICE FOR PRECONHUB - STATEMENT OF ADJUSTMENTS
// ============================================================
// File: Services/PdfService.cs
//
// Two-column Ontario SOA layout: Credit Vendor / Credit Purchaser
// ============================================================

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PreConHub.Models.Entities;

namespace PreConHub.Services
{
    // ===== PDF SERVICE INTERFACE =====
    public interface IPdfService
    {
        byte[] GenerateStatementOfAdjustments(
            PreConHub.Models.Entities.Unit unit,
            StatementOfAdjustments soa,
            List<Deposit> deposits,
            string purchaserName,
            string? coPurchaserNames = null);
    }

    // ===== PDF SERVICE IMPLEMENTATION =====
    public class PdfService : IPdfService
    {
        public PdfService()
        {
            // Set QuestPDF license (Community license is free for < $1M revenue)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateStatementOfAdjustments(
            PreConHub.Models.Entities.Unit unit,
            StatementOfAdjustments soa,
            List<Deposit> deposits,
            string purchaserName,
            string? coPurchaserNames = null)
        {
            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(9));

                    page.Header().Element(c => ComposeHeader(c, unit));
                    page.Content().Element(c => ComposeContent(c, unit, soa, deposits, purchaserName, coPurchaserNames));
                    page.Footer().Element(ComposeFooter);
                });
            });

            return document.GeneratePdf();
        }

        // ───────────────────────────────────────────────────
        // HEADER
        // ───────────────────────────────────────────────────

        private void ComposeHeader(IContainer container, PreConHub.Models.Entities.Unit unit)
        {
            container.Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("STATEMENT OF ADJUSTMENTS")
                            .FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                        col.Item().Text("Pre-Construction Closing Document")
                            .FontSize(9).FontColor(Colors.Grey.Darken1);
                    });

                    row.ConstantItem(120).Column(col =>
                    {
                        col.Item().AlignRight().Text("Document Date:")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                        col.Item().AlignRight().Text(DateTime.Now.ToString("MMMM dd, yyyy"))
                            .FontSize(10).Bold();
                    });
                });

                column.Item().PaddingTop(8).LineHorizontal(2).LineColor(Colors.Blue.Darken2);
            });
        }

        // ───────────────────────────────────────────────────
        // CONTENT (main layout)
        // ───────────────────────────────────────────────────

        private void ComposeContent(
            IContainer container,
            PreConHub.Models.Entities.Unit unit,
            StatementOfAdjustments soa,
            List<Deposit> deposits,
            string purchaserName,
            string? coPurchaserNames)
        {
            container.PaddingVertical(15).Column(column =>
            {
                // Property & Purchaser Info
                column.Item().Element(c => ComposePropertyInfo(c, unit, purchaserName, coPurchaserNames));
                column.Item().PaddingVertical(8);

                // Two-column SOA table
                column.Item().Element(c => ComposeSOATable(c, unit, soa, deposits));
                column.Item().PaddingVertical(8);

                // Financing & Cash Required
                column.Item().Element(c => ComposeFinancing(c, soa));
                column.Item().PaddingVertical(8);

                // Informational LTT section
                column.Item().Element(c => ComposeLTTInfo(c, soa, unit.IsFirstTimeBuyer));

                // Additional Consideration breakdown (NOTE 1)
                if (soa.AdditionalConsideration > 0)
                {
                    column.Item().PaddingVertical(8);
                    column.Item().Element(c => ComposeAdditionalConsiderationBreakdown(c, soa, unit));
                }
            });
        }

        // ───────────────────────────────────────────────────
        // PROPERTY & PURCHASER INFO
        // ───────────────────────────────────────────────────

        private void ComposePropertyInfo(IContainer container, PreConHub.Models.Entities.Unit unit, string purchaserName, string? coPurchaserNames)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten1).Padding(12).Column(column =>
            {
                column.Item().Text("PROPERTY & PURCHASER INFORMATION")
                    .FontSize(10).Bold().FontColor(Colors.Blue.Darken2);

                column.Item().PaddingTop(8).Row(row =>
                {
                    // Left Column - Property
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Property Details").Bold().FontSize(8).FontColor(Colors.Grey.Darken2);
                        col.Item().PaddingTop(4).Text($"Project: {unit.Project?.Name ?? "N/A"}").FontSize(9);
                        col.Item().Text($"Unit: {unit.UnitNumber}").FontSize(9);
                        if (!string.IsNullOrEmpty(unit.DwellingUnitNumber))
                            col.Item().Text($"Dwelling: {unit.DwellingUnitNumber}").FontSize(8).FontColor(Colors.Grey.Darken1);
                        if (!string.IsNullOrEmpty(unit.ParkingUnitNumber))
                            col.Item().Text($"Parking: {unit.ParkingUnitNumber}").FontSize(8).FontColor(Colors.Grey.Darken1);
                        if (!string.IsNullOrEmpty(unit.LockerUnitNumber))
                            col.Item().Text($"Locker: {unit.LockerUnitNumber}").FontSize(8).FontColor(Colors.Grey.Darken1);
                        col.Item().Text($"Type: {unit.UnitType} | {unit.Bedrooms} BR / {unit.Bathrooms} BA").FontSize(9);
                        col.Item().Text($"Size: {unit.SquareFootage:N0} sq ft").FontSize(9);
                        col.Item().Text($"Address: {unit.Project?.Address}, {unit.Project?.City}, ON").FontSize(9);
                        if (!string.IsNullOrEmpty(unit.Project?.CondoCorpNumber))
                            col.Item().Text(unit.Project.CondoCorpNumber).FontSize(8).FontColor(Colors.Grey.Darken1);
                        if (!string.IsNullOrEmpty(unit.Project?.TarionRegistrationNumber))
                            col.Item().PaddingTop(4).Text($"Tarion Builder Reg: {unit.Project.TarionRegistrationNumber}").FontSize(8);
                        if (!string.IsNullOrEmpty(unit.TarionUnitEnrolmentNumber))
                            col.Item().Text($"Unit Enrolment: {unit.TarionUnitEnrolmentNumber}").FontSize(8);
                        if (!string.IsNullOrEmpty(unit.Project?.BuilderHSTNumber))
                            col.Item().Text($"HST No: {unit.Project.BuilderHSTNumber}").FontSize(8);
                    });

                    // Right Column - Purchaser & Dates
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Purchaser Information").Bold().FontSize(8).FontColor(Colors.Grey.Darken2);
                        col.Item().PaddingTop(4).Text($"Purchaser: {purchaserName}").FontSize(9);
                        if (!string.IsNullOrEmpty(coPurchaserNames))
                        {
                            col.Item().Text($"Co-Purchaser(s): {coPurchaserNames}").FontSize(9);
                        }
                        col.Item().PaddingTop(8).Text("Important Dates").Bold().FontSize(8).FontColor(Colors.Grey.Darken2);
                        col.Item().PaddingTop(4).Text($"Occupancy Date: {unit.OccupancyDate?.ToString("MMM dd, yyyy") ?? "TBD"}").FontSize(9);
                        col.Item().Text($"Closing Date: {(unit.ClosingDate ?? unit.Project?.ClosingDate)?.ToString("MMM dd, yyyy") ?? "TBD"}").FontSize(9);
                    });
                });
            });
        }

        // ───────────────────────────────────────────────────
        // TWO-COLUMN SOA TABLE
        // ───────────────────────────────────────────────────

        private void ComposeSOATable(
            IContainer container,
            PreConHub.Models.Entities.Unit unit,
            StatementOfAdjustments soa,
            List<Deposit> deposits)
        {
            var closingDate = unit.ClosingDate ?? DateTime.UtcNow;

            container.Border(1).BorderColor(Colors.Grey.Lighten1).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(5);  // Description
                    columns.RelativeColumn(2);  // Credit Vendor
                    columns.RelativeColumn(2);  // Credit Purchaser
                });

                // ── Column Headers ──
                table.Cell().Background(Colors.Grey.Darken3).Padding(6)
                    .Text("ADJUSTMENT ITEM").FontSize(9).Bold().FontColor(Colors.White);
                table.Cell().Background(Colors.Grey.Darken3).Padding(6).AlignRight()
                    .Text("CREDIT VENDOR").FontSize(9).Bold().FontColor(Colors.White);
                table.Cell().Background(Colors.Grey.Darken3).Padding(6).AlignRight()
                    .Text("CREDIT PURCHASER").FontSize(9).Bold().FontColor(Colors.White);

                // ══════════════════════════════════════════
                // BREAKDOWN OF SALE PRICE
                // ══════════════════════════════════════════
                SectionHeader(table, "BREAKDOWN OF SALE PRICE");

                CellDesc(table, "Dwelling");
                CellVendor(table, soa.PurchasePrice);
                CellEmpty(table);

                if (soa.ParkingPrice > 0)
                {
                    CellDesc(table, "Parking");
                    CellVendor(table, soa.ParkingPrice);
                    CellEmpty(table);
                }
                if (soa.LockerPrice > 0)
                {
                    CellDesc(table, "Locker");
                    CellVendor(table, soa.LockerPrice);
                    CellEmpty(table);
                }

                CellDescBold(table, "Sale Price");
                table.Cell().PaddingHorizontal(6).PaddingVertical(3).AlignRight()
                    .Text($"${soa.SalePrice:N2}").FontSize(9).Bold().FontColor(Colors.Blue.Darken2);
                CellEmpty(table);

                // ══════════════════════════════════════════
                // ADJUSTED SALE PRICE
                // ══════════════════════════════════════════
                SectionHeader(table, "ADJUSTED SALE PRICE");

                CellDesc(table, "Sale Price");
                CellVendor(table, soa.SalePrice);
                CellEmpty(table);

                CellDesc(table, "Additional Consideration (fees + HST)");
                CellVendor(table, soa.AdditionalConsideration);
                CellEmpty(table);

                CellDescBold(table, "Total Sale Price");
                table.Cell().PaddingHorizontal(6).PaddingVertical(3).AlignRight()
                    .Text($"${soa.TotalSalePrice:N2}").FontSize(9).Bold().FontColor(Colors.Blue.Darken2);
                CellEmpty(table);

                CellDesc(table, "Federal HST (5%)");
                CellVendor(table, soa.FederalHST);
                CellEmpty(table);

                CellDesc(table, "Provincial HST (8%)");
                CellVendor(table, soa.ProvincialHST);
                CellEmpty(table);

                if (soa.HSTRebateFederal > 0)
                {
                    CellDesc(table, "Less: Federal New Housing Rebate");
                    CellEmpty(table);
                    CellPurchaser(table, soa.HSTRebateFederal);
                }
                if (soa.HSTRebateOntario > 0)
                {
                    CellDesc(table, "Less: Ontario New Housing Rebate");
                    CellEmpty(table);
                    CellPurchaser(table, soa.HSTRebateOntario);
                }

                // Net Sale Price highlight row
                table.Cell().Background(Colors.Blue.Lighten4).Padding(6)
                    .Text("NET SALE PRICE").FontSize(10).Bold();
                table.Cell().Background(Colors.Blue.Lighten4).Padding(6).AlignRight()
                    .Text($"${soa.NetSalePrice:N2}").FontSize(10).Bold().FontColor(Colors.Blue.Darken2);
                table.Cell().Background(Colors.Blue.Lighten4).Padding(6).Text("");

                // ══════════════════════════════════════════
                // DEPOSITS
                // ══════════════════════════════════════════
                var paidDeposits = deposits.Where(d => d.IsPaid).OrderBy(d => d.PaidDate).ToList();
                if (paidDeposits.Any())
                {
                    // Categorize deposits: pre-occupancy vs at-occupancy (balance paid)
                    var preOccDeposits = paidDeposits
                        .Where(d => d.PaidDate.HasValue && unit.OccupancyDate.HasValue && d.PaidDate.Value < unit.OccupancyDate.Value)
                        .ToList();
                    var atOccDeposits = paidDeposits
                        .Where(d => d.PaidDate.HasValue && unit.OccupancyDate.HasValue && d.PaidDate.Value >= unit.OccupancyDate.Value)
                        .ToList();

                    // If no occupancy date, show all together
                    if (!unit.OccupancyDate.HasValue)
                    {
                        preOccDeposits = paidDeposits;
                        atOccDeposits = new List<Deposit>();
                    }

                    if (preOccDeposits.Any())
                    {
                        SectionHeader(table, "TOTAL DEPOSITS PAID");
                        foreach (var dep in preOccDeposits)
                        {
                            var label = dep.PaidDate.HasValue
                                ? $"{dep.PaidDate.Value:MMM dd, yyyy} \u2014 {dep.DepositName}"
                                : dep.DepositName;
                            CellDesc(table, label);
                            CellEmpty(table);
                            CellPurchaser(table, dep.Amount);
                        }
                    }

                    // Upgrade charges paid (Credit Purchaser)
                    if (soa.Upgrades > 0 && unit.UpgradePaidDate.HasValue)
                    {
                        SectionHeader(table, "UPGRADE CHARGES PAID");
                        CellDescBold(table, "Upgrade Charges Paid");
                        CellEmpty(table);
                        CellPurchaserBold(table, soa.Upgrades);
                    }

                    // Balance paid at occupancy
                    if (atOccDeposits.Any())
                    {
                        SectionHeader(table, "UNADJUSTED BALANCE PAID ON OCCUPANCY DATE");
                        foreach (var dep in atOccDeposits)
                        {
                            var label = dep.PaidDate.HasValue
                                ? $"{dep.PaidDate.Value:MMM dd, yyyy} \u2014 {dep.DepositName}"
                                : dep.DepositName;
                            CellDesc(table, label);
                            CellEmpty(table);
                            CellPurchaser(table, dep.Amount);
                        }
                    }
                }

                // ══════════════════════════════════════════
                // INTEREST ON DEPOSITS
                // ══════════════════════════════════════════
                if (soa.DepositInterest > 0)
                {
                    SectionHeader(table, "INTEREST ON DEPOSITS");

                    foreach (var dep in paidDeposits.Where(d => d.PaidDate.HasValue))
                    {
                        var depositDate = dep.PaidDate!.Value;

                        if (dep.InterestPeriods != null && dep.InterestPeriods.Any())
                        {
                            foreach (var period in dep.InterestPeriods.OrderBy(p => p.PeriodStart))
                            {
                                var effStart = depositDate > period.PeriodStart ? depositDate : period.PeriodStart;
                                var effEnd = closingDate < period.PeriodEnd ? closingDate : period.PeriodEnd;

                                if (effEnd > effStart)
                                {
                                    var days = (effEnd - effStart).Days;
                                    var interest = dep.Amount * (period.AnnualRate / 100m) * (days / 365m);
                                    CellDesc(table, $"    {dep.DepositName}: {effStart:MMM d, yyyy} \u2013 {effEnd:MMM d, yyyy} @ {period.AnnualRate:F3}% ({days} days)");
                                    CellEmpty(table);
                                    CellPurchaser(table, Math.Round(interest, 2));
                                }
                            }
                        }
                        else if (dep.IsInterestEligible && dep.InterestRate.HasValue && dep.InterestRate.Value > 0)
                        {
                            var days = (closingDate - depositDate).Days;
                            var interest = dep.Amount * dep.InterestRate.Value * (days / 365m);
                            CellDesc(table, $"    {dep.DepositName}: {depositDate:MMM d, yyyy} \u2013 {closingDate:MMM d, yyyy} ({days} days)");
                            CellEmpty(table);
                            CellPurchaser(table, Math.Round(interest, 2));
                        }
                    }

                    // Total deposit interest
                    CellDescBold(table, "Total Deposit Interest");
                    CellEmpty(table);
                    CellPurchaserBold(table, soa.DepositInterest);
                }

                // Interest on deposit interest
                if (soa.InterestOnDepositInterest > 0)
                {
                    CellDesc(table, $"Interest on Deposit Interest ({unit.OccupancyDate?.ToString("MMM d, yyyy") ?? "N/A"} to {closingDate:MMM d, yyyy})");
                    CellEmpty(table);
                    CellPurchaser(table, soa.InterestOnDepositInterest);
                }

                // ══════════════════════════════════════════
                // ADJUSTMENTS
                // ══════════════════════════════════════════
                SectionHeader(table, "ADJUSTMENTS");

                // Land Taxes
                if (soa.PropertyTaxAdjustment > 0)
                {
                    var closingYearTax = unit.ActualAnnualLandTax ?? unit.PurchasePrice * 0.01m;
                    var estFlag = unit.ActualAnnualLandTax == null ? " *est" : "";
                    var occDate = unit.OccupancyDate ?? closingDate;

                    if (unit.PriorYearAnnualLandTax.HasValue && occDate.Year < closingDate.Year)
                    {
                        // Multi-year: show both years
                        CellDesc(table, $"Land Taxes {occDate.Year} (Annual: ${unit.PriorYearAnnualLandTax.Value:N2}; prorated from {occDate:MMM d})");
                        var priorYearDays = DateTime.IsLeapYear(occDate.Year) ? 366 : 365;
                        var occYearDays = (new DateTime(occDate.Year, 12, 31) - occDate).Days + 1;
                        var priorYearAmount = Math.Round(unit.PriorYearAnnualLandTax.Value * occYearDays / priorYearDays, 2);
                        CellVendor(table, priorYearAmount);
                        CellEmpty(table);

                        CellDesc(table, $"Land Taxes {closingDate.Year} (Annual: ${closingYearTax:N2}; full year)");
                        CellVendor(table, closingYearTax);
                        CellEmpty(table);
                    }
                    else
                    {
                        // Single year or same year
                        var daysInYear = DateTime.IsLeapYear(closingDate.Year) ? 366 : 365;
                        var purchaserDays = (new DateTime(closingDate.Year, 12, 31) - occDate).Days + 1;
                        CellDesc(table, $"Land Taxes (Annual: ${closingYearTax:N2}; {purchaserDays}/{daysInYear} days{estFlag})");
                        CellVendor(table, soa.PropertyTaxAdjustment);
                        CellEmpty(table);
                    }
                }

                // Common Expenses
                if (soa.CommonExpenseAdjustment > 0)
                {
                    var dwellingFee = unit.ActualMonthlyMaintenanceFee ?? unit.SquareFootage * 0.60m;
                    var totalMonthlyFee = dwellingFee + (unit.ParkingMonthlyCommonExpense ?? 0) + (unit.LockerMonthlyCommonExpense ?? 0);
                    var daysInMonth = DateTime.DaysInMonth(closingDate.Year, closingDate.Month);
                    var daysRemaining = daysInMonth - closingDate.Day;
                    var estFlag = unit.ActualMonthlyMaintenanceFee == null ? " *est" : "";
                    CellDesc(table, $"Common Expenses (Monthly: ${totalMonthlyFee:N2}; {daysRemaining}/{daysInMonth} days{estFlag})");
                    CellVendor(table, soa.CommonExpenseAdjustment);
                    CellEmpty(table);
                }

                // Reserve Fund Contribution
                if (soa.ReserveFundContribution > 0)
                {
                    CellDesc(table, "Reserve Fund Contribution (2 months common expenses)");
                    CellVendor(table, soa.ReserveFundContribution);
                    CellEmpty(table);
                }

                // Common Expenses First Month
                if (soa.CommonExpensesFirstMonth > 0)
                {
                    CellDesc(table, "Common Expenses \u2014 First Month After Closing");
                    CellVendor(table, soa.CommonExpensesFirstMonth);
                    CellEmpty(table);
                }

                // Occupancy Fees Chargeable (Credit Vendor)
                if (soa.OccupancyFeesChargeable > 0)
                {
                    CellDesc(table, "Occupancy Fees Chargeable");
                    CellVendor(table, soa.OccupancyFeesChargeable);
                    CellEmpty(table);
                }

                // Occupancy Fees Paid (Credit Purchaser)
                if (soa.OccupancyFeesPaid > 0)
                {
                    CellDesc(table, "Occupancy Fees Paid");
                    CellEmpty(table);
                    CellPurchaser(table, soa.OccupancyFeesPaid);
                }

                // Occupancy Fee Tax Portion Refund (Credit Purchaser)
                if (soa.OccupancyFeeTaxRefund > 0)
                {
                    CellDesc(table, "Re-adjustment of Tax Portion of Occupancy Fee");
                    CellEmpty(table);
                    CellPurchaser(table, soa.OccupancyFeeTaxRefund);
                }

                // Occupancy Fee Closing Month Adjustment (Credit Purchaser)
                if (soa.OccupancyFeeClosingMonthAdj > 0)
                {
                    CellDesc(table, "Occupancy Fee Closing Month Adjustment");
                    CellEmpty(table);
                    CellPurchaser(table, soa.OccupancyFeeClosingMonthAdj);
                }

                // Security Deposit Refund (Credit Purchaser)
                if (soa.SecurityDepositRefund > 0)
                {
                    CellDesc(table, "Security Deposit Refund");
                    CellEmpty(table);
                    CellPurchaser(table, soa.SecurityDepositRefund);
                }

                // ══════════════════════════════════════════
                // BUILDER CREDITS
                // ══════════════════════════════════════════
                if (soa.BuilderCredits > 0 || soa.OtherCredits > 0)
                {
                    SectionHeader(table, "CREDITS");

                    if (soa.BuilderCredits > 0)
                    {
                        CellDesc(table, "Builder Credits");
                        CellEmpty(table);
                        CellPurchaser(table, soa.BuilderCredits);

                        // Sub-detail
                        if (soa.DesignCredits > 0)
                            CellInfo(table, $"    Design Credits: ${soa.DesignCredits:N2}");
                        if (soa.FreeUpgradesValue > 0)
                            CellInfo(table, $"    Free Upgrades: ${soa.FreeUpgradesValue:N2}");
                        if (soa.CashBackIncentives > 0)
                            CellInfo(table, $"    Cash Back Incentives: ${soa.CashBackIncentives:N2}");
                    }
                    if (soa.OtherCredits > 0)
                    {
                        CellDesc(table, "Other Credits");
                        CellEmpty(table);
                        CellPurchaser(table, soa.OtherCredits);
                    }
                }

                // ══════════════════════════════════════════
                // TOTALS
                // ══════════════════════════════════════════
                // Total Vendor
                table.Cell().Background(Colors.Blue.Lighten5).Padding(6)
                    .Text("TOTAL CREDIT VENDOR").FontSize(10).Bold();
                table.Cell().Background(Colors.Blue.Lighten5).Padding(6).AlignRight()
                    .Text($"${soa.TotalVendorCredits:N2}").FontSize(10).Bold().FontColor(Colors.Blue.Darken2);
                table.Cell().Background(Colors.Blue.Lighten5).Padding(6).Text("");

                // Total Purchaser
                table.Cell().Background(Colors.Green.Lighten5).Padding(6)
                    .Text("TOTAL CREDIT PURCHASER").FontSize(10).Bold();
                table.Cell().Background(Colors.Green.Lighten5).Padding(6).Text("");
                table.Cell().Background(Colors.Green.Lighten5).Padding(6).AlignRight()
                    .Text($"${soa.TotalPurchaserCredits:N2}").FontSize(10).Bold().FontColor(Colors.Green.Darken2);

                // Balance Due
                table.Cell().Background(Colors.Blue.Darken2).Padding(8)
                    .Text("BALANCE DUE ON CLOSING").FontSize(11).Bold().FontColor(Colors.White);
                table.Cell().ColumnSpan(2).Background(Colors.Blue.Darken2).Padding(8).AlignRight()
                    .Text($"${soa.BalanceDueOnClosing:N2}").FontSize(13).Bold().FontColor(Colors.White);
            });
        }

        // ───────────────────────────────────────────────────
        // FINANCING & CASH REQUIRED
        // ───────────────────────────────────────────────────

        private void ComposeFinancing(IContainer container, StatementOfAdjustments soa)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(column =>
            {
                column.Item().Text("FINANCING").FontSize(10).Bold().FontColor(Colors.Grey.Darken2);

                column.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text("Mortgage Amount:").FontSize(9);
                    row.ConstantItem(120).AlignRight().Text($"${soa.MortgageAmount:N2}").FontSize(9);
                });

                var cashRequired = soa.CashRequiredToClose;
                var textColor = cashRequired <= 0 ? Colors.Green.Darken3 : Colors.Red.Darken3;
                var statusText = cashRequired <= 0 ? "FULLY FUNDED" : "CASH REQUIRED";

                column.Item().PaddingTop(8).Border(1).BorderColor(textColor).Padding(8).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("CASH REQUIRED TO CLOSE").FontSize(10).Bold().FontColor(textColor);
                        c.Item().PaddingTop(2).Text("(Balance Due \u2212 Mortgage)").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                    row.ConstantItem(140).AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text($"${Math.Abs(cashRequired):N2}")
                            .FontSize(14).Bold().FontColor(textColor);
                        c.Item().AlignRight().Text(statusText).FontSize(9).Bold().FontColor(textColor);
                    });
                });
            });
        }

        // ───────────────────────────────────────────────────
        // ADDITIONAL CONSIDERATION BREAKDOWN
        // ───────────────────────────────────────────────────

        private void ComposeAdditionalConsiderationBreakdown(IContainer container, StatementOfAdjustments soa, PreConHub.Models.Entities.Unit unit)
        {
            container.Background(Colors.Grey.Lighten4).Border(1).BorderColor(Colors.Grey.Lighten2)
                .Padding(10).Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Additional Consideration Breakdown")
                        .FontSize(9).Bold().FontColor(Colors.Grey.Darken2);
                    row.ConstantItem(100).AlignRight().Text($"${soa.AdditionalConsideration:N2}")
                        .FontSize(9).Bold().Underline();
                });

                column.Item().PaddingTop(5);

                void FeeRow(string name, decimal amount)
                {
                    if (amount <= 0) return;
                    column.Item().Row(r =>
                    {
                        r.RelativeItem().Text(name).FontSize(8);
                        r.ConstantItem(80).AlignRight().Text($"${amount:N2}").FontSize(8);
                    });
                }

                FeeRow("Upgrade Charges", soa.Upgrades > 0 ? Math.Round(soa.Upgrades * 1.13m, 2) : 0);
                FeeRow("Tarion Warranty", soa.TarionFee > 0 ? Math.Round(soa.TarionFee * 1.13m, 2) : 0);
                FeeRow("Meters / Utility Connection", soa.UtilityConnectionFees > 0 ? Math.Round(soa.UtilityConnectionFees * 1.13m, 2) : 0);
                FeeRow("Development Charges", soa.DevelopmentCharges > 0 ? Math.Round(soa.DevelopmentCharges * 1.13m, 2) : 0);
                FeeRow("Education Development Charges", soa.EducationDevelopmentCharges > 0 ? Math.Round(soa.EducationDevelopmentCharges * 1.13m, 2) : 0);
                FeeRow("Parkland Levy", soa.ParklandLevy > 0 ? Math.Round(soa.ParklandLevy * 1.13m, 2) : 0);
                FeeRow("Community Benefit Charges", soa.CommunityBenefitCharges > 0 ? Math.Round(soa.CommunityBenefitCharges * 1.13m, 2) : 0);
                FeeRow("Schedule B Closing Fees", soa.ScheduleBClosingFees > 0 ? Math.Round(soa.ScheduleBClosingFees * 1.13m, 2) : 0);
                FeeRow("HCRA Fee", soa.HCRAFee);
                FeeRow("Electronic Registration Fee", soa.ElectronicRegFee);
                FeeRow("Status Certificate", soa.StatusCertFee);
                FeeRow("Transaction Levy (LAWPRO)", soa.TransactionLevyFee);
                FeeRow("Legal Fees", soa.LegalFeesEstimate > 0 ? Math.Round(soa.LegalFeesEstimate * 1.13m, 2) : 0);
                FeeRow("Other Charges", soa.OtherDebits > 0 ? Math.Round(soa.OtherDebits * 1.13m, 2) : 0);
            });
        }

        // ───────────────────────────────────────────────────
        // INFORMATIONAL LTT (not included in balance)
        // ───────────────────────────────────────────────────

        private void ComposeLTTInfo(IContainer container, StatementOfAdjustments soa, bool isFirstTimeBuyer)
        {
            if (soa.LandTransferTax <= 0 && soa.TorontoLandTransferTax <= 0) return;

            container.Background(Colors.Grey.Lighten4).Border(1).BorderColor(Colors.Grey.Lighten2)
                .Padding(10).Column(column =>
            {
                column.Item().Text("INFORMATIONAL \u2014 LAND TRANSFER TAX (paid at registration, not included in balance)")
                    .FontSize(9).Bold().FontColor(Colors.Grey.Darken2);

                column.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text("Ontario LTT:").FontSize(9);
                    row.ConstantItem(100).AlignRight().Text($"${soa.LandTransferTax:N2}").FontSize(9);
                });

                if (soa.TorontoLandTransferTax > 0)
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Toronto MLTT:").FontSize(9);
                        row.ConstantItem(100).AlignRight().Text($"${soa.TorontoLandTransferTax:N2}").FontSize(9);
                    });
                }

                var combinedLtt = soa.LandTransferTax + soa.TorontoLandTransferTax;
                column.Item().PaddingTop(3).Row(row =>
                {
                    row.RelativeItem().Text("Combined Net LTT:").FontSize(9).Bold();
                    row.ConstantItem(100).AlignRight().Text($"${combinedLtt:N2}").FontSize(9).Bold();
                });

                if (isFirstTimeBuyer)
                {
                    column.Item().PaddingTop(3).Text("First-Time Home Buyer LTT rebates applied: Ontario (up to $4,000) + Toronto MLTT (up to $4,475).")
                        .FontSize(7).FontColor(Colors.Green.Darken2).Italic();
                }

                column.Item().PaddingTop(3).Text("Land Transfer Tax is paid by the purchaser's lawyer at land registration and is not included in the SOA balance.")
                    .FontSize(7).FontColor(Colors.Grey.Medium).Italic();
            });
        }

        // ───────────────────────────────────────────────────
        // TABLE CELL HELPERS
        // ───────────────────────────────────────────────────

        private static void SectionHeader(TableDescriptor table, string title)
        {
            table.Cell().ColumnSpan(3).Background(Colors.Grey.Lighten2).PaddingHorizontal(6).PaddingVertical(4)
                .Text(title).FontSize(9).Bold().FontColor(Colors.Grey.Darken3);
        }

        private static void CellDesc(TableDescriptor table, string text)
        {
            table.Cell().PaddingHorizontal(6).PaddingVertical(3).Text(text).FontSize(9);
        }

        private static void CellDescBold(TableDescriptor table, string text)
        {
            table.Cell().PaddingHorizontal(6).PaddingVertical(3).Text(text).FontSize(9).Bold();
        }

        private static void CellVendor(TableDescriptor table, decimal amount)
        {
            table.Cell().PaddingHorizontal(6).PaddingVertical(3).AlignRight()
                .Text($"${amount:N2}").FontSize(9).FontColor(Colors.Blue.Darken2);
        }

        private static void CellPurchaser(TableDescriptor table, decimal amount)
        {
            table.Cell().PaddingHorizontal(6).PaddingVertical(3).AlignRight()
                .Text($"${amount:N2}").FontSize(9).FontColor(Colors.Green.Darken2);
        }

        private static void CellPurchaserBold(TableDescriptor table, decimal amount)
        {
            table.Cell().PaddingHorizontal(6).PaddingVertical(3).AlignRight()
                .Text($"${amount:N2}").FontSize(9).Bold().FontColor(Colors.Green.Darken2);
        }

        private static void CellEmpty(TableDescriptor table)
        {
            table.Cell().PaddingHorizontal(6).PaddingVertical(3).Text("");
        }

        private static void CellInfo(TableDescriptor table, string text)
        {
            table.Cell().ColumnSpan(3).PaddingHorizontal(6).PaddingVertical(1)
                .Text(text).FontSize(8).FontColor(Colors.Grey.Darken1);
        }

        // ───────────────────────────────────────────────────
        // FOOTER
        // ───────────────────────────────────────────────────

        private void ComposeFooter(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                column.Item().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem().Text(text =>
                    {
                        text.Span("Generated by ").FontSize(8).FontColor(Colors.Grey.Medium);
                        text.Span("PreConHub").FontSize(8).Bold().FontColor(Colors.Blue.Darken2);
                        text.Span($" on {DateTime.Now:MMMM dd, yyyy 'at' h:mm tt}").FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                    row.ConstantItem(100).AlignRight().Text(text =>
                    {
                        text.Span("Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                        text.CurrentPageNumber().FontSize(8);
                        text.Span(" of ").FontSize(8).FontColor(Colors.Grey.Medium);
                        text.TotalPages().FontSize(8);
                    });
                });

                column.Item().PaddingTop(5).Text("This document is for informational purposes only. Final amounts may vary. Please consult with your lawyer for official closing figures.")
                    .FontSize(7).FontColor(Colors.Grey.Medium).Italic();
            });
        }
    }
}
