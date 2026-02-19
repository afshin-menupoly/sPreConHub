// ============================================================
// PDF SERVICE FOR PRECONHUB - STATEMENT OF ADJUSTMENTS
// ============================================================
// File: Services/PdfService.cs
// 
// REQUIRED NUGET PACKAGE:
// Install-Package QuestPDF
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
                    page.DefaultTextStyle(x => x.FontSize(10));

                    // Header
                    page.Header().Element(c => ComposeHeader(c, unit));

                    // Content
                    page.Content().Element(c => ComposeContent(c, unit, soa, deposits, purchaserName, coPurchaserNames));

                    // Footer
                    page.Footer().Element(ComposeFooter);
                });
            });

            return document.GeneratePdf();
        }

        private void ComposeHeader(IContainer container, PreConHub.Models.Entities.Unit unit)
        {
            container.Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("STATEMENT OF ADJUSTMENTS")
                            .FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                        col.Item().Text("Pre-Construction Closing Document")
                            .FontSize(10).FontColor(Colors.Grey.Darken1);
                    });

                    row.ConstantItem(120).Column(col =>
                    {
                        col.Item().AlignRight().Text("Document Date:")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                        col.Item().AlignRight().Text(DateTime.Now.ToString("MMMM dd, yyyy"))
                            .FontSize(10).Bold();
                    });
                });

                column.Item().PaddingTop(10).LineHorizontal(2).LineColor(Colors.Blue.Darken2);
            });
        }

        private void ComposeContent(
            IContainer container,
            PreConHub.Models.Entities.Unit unit,
            StatementOfAdjustments soa,
            List<Deposit> deposits,
            string purchaserName,
            string? coPurchaserNames)
        {
            container.PaddingVertical(20).Column(column =>
            {
                // Property & Purchaser Info
                column.Item().Element(c => ComposePropertyInfo(c, unit, purchaserName, coPurchaserNames));

                column.Item().PaddingVertical(15);

                // Purchase Price Section
                column.Item().Element(c => ComposePurchaseSection(c, unit, soa));

                column.Item().PaddingVertical(10);

                // Debits Section
                column.Item().Element(c => ComposeDebitsSection(c, soa));

                column.Item().PaddingVertical(10);

                // Credits Section
                column.Item().Element(c => ComposeCreditsSection(c, deposits, soa));

                column.Item().PaddingVertical(10);

                // Summary Section
                column.Item().Element(c => ComposeSummarySection(c, soa));

                column.Item().PaddingVertical(15);

                // Mortgage & Cash Required
                column.Item().Element(c => ComposeFinalSection(c, soa));
            });
        }

        private void ComposePropertyInfo(IContainer container, PreConHub.Models.Entities.Unit unit, string purchaserName, string? coPurchaserNames)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten1).Padding(15).Column(column =>
            {
                column.Item().Text("PROPERTY & PURCHASER INFORMATION")
                    .FontSize(11).Bold().FontColor(Colors.Blue.Darken2);

                column.Item().PaddingTop(10).Row(row =>
                {
                    // Left Column - Property
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Property Details").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                        col.Item().PaddingTop(5).Text($"Project: {unit.Project?.Name ?? "N/A"}").FontSize(10);
                        col.Item().Text($"Unit: {unit.UnitNumber}").FontSize(10);
                        col.Item().Text($"Type: {unit.UnitType} | {unit.Bedrooms} BR / {unit.Bathrooms} BA").FontSize(10);
                        col.Item().Text($"Size: {unit.SquareFootage:N0} sq ft").FontSize(10);
                        col.Item().Text($"Address: {unit.Project?.Address}, {unit.Project?.City}, ON").FontSize(10);
                    });

                    // Right Column - Purchaser & Dates
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Purchaser Information").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                        col.Item().PaddingTop(5).Text($"Purchaser: {purchaserName}").FontSize(10);
                        if (!string.IsNullOrEmpty(coPurchaserNames))
                        {
                            col.Item().Text($"Co-Purchaser(s): {coPurchaserNames}").FontSize(10);
                        }
                        col.Item().PaddingTop(10).Text("Important Dates").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                        col.Item().PaddingTop(5).Text($"Occupancy Date: {unit.OccupancyDate?.ToString("MMM dd, yyyy") ?? "TBD"}").FontSize(10);
                        col.Item().Text($"Closing Date: {(unit.ClosingDate ?? unit.Project?.ClosingDate)?.ToString("MMM dd, yyyy") ?? "TBD"}").FontSize(10);
                    });
                });
            });
        }

        private void ComposePurchaseSection(IContainer container, PreConHub.Models.Entities.Unit unit, StatementOfAdjustments soa)
        {
            container.Column(column =>
            {
                column.Item().Background(Colors.Blue.Darken2).Padding(8)
                    .Text("PURCHASE PRICE").FontSize(11).Bold().FontColor(Colors.White);

                column.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                    });

                    // Purchase Price
                    table.Cell().Padding(8).Text("Base Purchase Price").FontSize(10);
                    table.Cell().Padding(8).AlignRight().Text($"${unit.PurchasePrice:N2}").FontSize(10).Bold();
                });
            });
        }

        private void ComposeDebitsSection(IContainer container, StatementOfAdjustments soa)
        {
            container.Column(column =>
            {
                column.Item().Background(Colors.Red.Darken2).Padding(8)
                    .Text("DEBITS (Amounts Owing)").FontSize(11).Bold().FontColor(Colors.White);

                column.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                    });

                    // Header
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Description").FontSize(9).Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Amount").FontSize(9).Bold();

                    // Ontario Land Transfer Tax
                    if (soa.LandTransferTax > 0)
                    {
                        table.Cell().Padding(5).Text("Ontario Land Transfer Tax").FontSize(9);
                        table.Cell().Padding(5).AlignRight().Text($"${soa.LandTransferTax:N2}").FontSize(9);
                    }

                    // Toronto Land Transfer Tax
                    if (soa.TorontoLandTransferTax > 0)
                    {
                        table.Cell().Padding(5).Text("Toronto Land Transfer Tax").FontSize(9);
                        table.Cell().Padding(5).AlignRight().Text($"${soa.TorontoLandTransferTax:N2}").FontSize(9);
                    }

                    // Tarion Fee
                    if (soa.TarionFee > 0)
                    {
                        table.Cell().Padding(5).Text("Tarion Warranty Enrollment Fee").FontSize(9);
                        table.Cell().Padding(5).AlignRight().Text($"${soa.TarionFee:N2}").FontSize(9);
                    }

                    // Development Charges
                    if (soa.DevelopmentCharges > 0)
                    {
                        table.Cell().Padding(5).Text("Development Charges Levy").FontSize(9);
                        table.Cell().Padding(5).AlignRight().Text($"${soa.DevelopmentCharges:N2}").FontSize(9);
                    }

                    // Utility Connection Fees
                    if (soa.UtilityConnectionFees > 0)
                    {
                        table.Cell().Padding(5).Text("Utility Connection Fees").FontSize(9);
                        table.Cell().Padding(5).AlignRight().Text($"${soa.UtilityConnectionFees:N2}").FontSize(9);
                    }

                    // Occupancy Fees
                    if (soa.OccupancyFeesOwing > 0)
                    {
                        table.Cell().Padding(5).Text("Occupancy Fees Owing").FontSize(9);
                        table.Cell().Padding(5).AlignRight().Text($"${soa.OccupancyFeesOwing:N2}").FontSize(9);
                    }

                    // Parking
                    if (soa.ParkingPrice > 0)
                    {
                        table.Cell().Padding(5).Text("Parking").FontSize(9);
                        table.Cell().Padding(5).AlignRight().Text($"${soa.ParkingPrice:N2}").FontSize(9);
                    }

                    // Locker
                    if (soa.LockerPrice > 0)
                    {
                        table.Cell().Padding(5).Text("Locker").FontSize(9);
                        table.Cell().Padding(5).AlignRight().Text($"${soa.LockerPrice:N2}").FontSize(9);
                    }

                    // Upgrades
                    if (soa.Upgrades > 0)
                    {
                        table.Cell().Padding(5).Text("Upgrades").FontSize(9);
                        table.Cell().Padding(5).AlignRight().Text($"${soa.Upgrades:N2}").FontSize(9);
                    }

                    // Legal Fees Estimate
                    if (soa.LegalFeesEstimate > 0)
                    {
                        table.Cell().Padding(5).Text("Legal Fees (Est.)").FontSize(9);
                        table.Cell().Padding(5).AlignRight().Text($"${soa.LegalFeesEstimate:N2}").FontSize(9);
                    }

                    // Other Debits
                    if (soa.OtherDebits > 0)
                    {
                        table.Cell().Padding(5).Text("Other Debits").FontSize(9);
                        table.Cell().Padding(5).AlignRight().Text($"${soa.OtherDebits:N2}").FontSize(9);
                    }

                    // Total Debits
                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                        .Text("TOTAL DEBITS").FontSize(10).Bold();
                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                        .AlignRight().Text($"${soa.TotalDebits:N2}").FontSize(10).Bold().FontColor(Colors.Red.Darken2);
                });
            });
        }

        private void ComposeCreditsSection(IContainer container, List<Deposit> deposits, StatementOfAdjustments soa)
        {
            container.Column(column =>
            {
                column.Item().Background(Colors.Green.Darken2).Padding(8)
                    .Text("CREDITS (Amounts Paid)").FontSize(11).Bold().FontColor(Colors.White);

                column.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                    });

                    // Header
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Description").FontSize(9).Bold();
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Amount").FontSize(9).Bold();

                    // Deposits Paid
                    table.Cell().Padding(5).Text("Deposits Paid").FontSize(9);
                    table.Cell().Padding(5).AlignRight().Text($"${soa.DepositsPaid:N2}").FontSize(9).FontColor(Colors.Green.Darken2);

                    // Deposit Interest
                    if (soa.DepositInterest > 0)
                    {
                        table.Cell().Padding(5).Text("Deposit Interest").FontSize(9);
                        table.Cell().Padding(5).AlignRight().Text($"${soa.DepositInterest:N2}").FontSize(9).FontColor(Colors.Green.Darken2);
                    }

                    // Builder Credits
                    if (soa.BuilderCredits > 0)
                    {
                        table.Cell().Padding(5).Text("Builder Credits").FontSize(9);
                        table.Cell().Padding(5).AlignRight().Text($"${soa.BuilderCredits:N2}").FontSize(9).FontColor(Colors.Green.Darken2);
                    }

                    // Other Credits
                    if (soa.OtherCredits > 0)
                    {
                        table.Cell().Padding(5).Text("Other Credits").FontSize(9);
                        table.Cell().Padding(5).AlignRight().Text($"${soa.OtherCredits:N2}").FontSize(9).FontColor(Colors.Green.Darken2);
                    }

                    // Total Credits
                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                        .Text("TOTAL CREDITS").FontSize(10).Bold();
                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                        .AlignRight().Text($"${soa.TotalCredits:N2}").FontSize(10).Bold().FontColor(Colors.Green.Darken2);
                });
            });
        }

        private void ComposeSummarySection(IContainer container, StatementOfAdjustments soa)
        {
            container.Border(2).BorderColor(Colors.Blue.Darken2).Background(Colors.Blue.Lighten5).Padding(15).Column(column =>
            {
                column.Item().Text("BALANCE DUE ON CLOSING")
                    .FontSize(14).Bold().FontColor(Colors.Blue.Darken2);

                column.Item().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem().Text("Total Debits - Total Credits").FontSize(10);
                    row.ConstantItem(120).AlignRight().Text($"${soa.BalanceDueOnClosing:N2}")
                        .FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                });
            });
        }

        private void ComposeFinalSection(IContainer container, StatementOfAdjustments soa)
        {
            container.Column(column =>
            {
                // Mortgage Info
                column.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("FINANCING").FontSize(11).Bold().FontColor(Colors.Grey.Darken2);
                        col.Item().PaddingTop(5).Text($"Mortgage Amount: ${soa.MortgageAmount:N2}").FontSize(10);
                    });
                });

                column.Item().PaddingTop(10);

                // Cash Required
                var cashRequired = soa.CashRequiredToClose;
                var bgColor = cashRequired <= 0 ? Colors.Green.Lighten4 : Colors.Red.Lighten4;
                var textColor = cashRequired <= 0 ? Colors.Green.Darken3 : Colors.Red.Darken3;
                var statusText = cashRequired <= 0 ? "FULLY FUNDED" : "CASH REQUIRED";

                column.Item().Border(2).BorderColor(textColor).Background(bgColor).Padding(15).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("CASH REQUIRED TO CLOSE").FontSize(12).Bold().FontColor(textColor);
                            c.Item().PaddingTop(3).Text("(Balance Due - Mortgage Amount)").FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(150).AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Text($"${Math.Abs(cashRequired):N2}")
                                .FontSize(18).Bold().FontColor(textColor);
                            c.Item().AlignRight().Text(statusText).FontSize(10).Bold().FontColor(textColor);
                        });
                    });
                });
            });
        }

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
