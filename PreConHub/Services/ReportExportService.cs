using PreConHub.Models.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ClosedXML.Excel;
using CsvHelper;
using System.Globalization;

namespace PreConHub.Services
{
    public interface IReportExportService
    {
        byte[] GenerateProjectReportPDF(string projectName, string projectAddress, List<ReportUnitItem> units);
        byte[] GenerateProjectReportExcel(string projectName, string projectAddress, List<ReportUnitItem> units);
        byte[] GenerateProjectReportCSV(List<ReportUnitItem> units);
    }

    public class ReportExportService : IReportExportService
    {
        public ReportExportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // ================================================================
        // PDF EXPORT - QuestPDF
        // ================================================================
        public byte[] GenerateProjectReportPDF(string projectName, string projectAddress, List<ReportUnitItem> units)
        {
            var totalUnits = units.Count;
            var closingClean = units.Count(u => u.Recommendation == Models.Entities.ClosingRecommendation.ProceedToClose);
            var needDiscount = units.Count(u => u.Recommendation == Models.Entities.ClosingRecommendation.CloseWithDiscount);
            var needVTB = units.Count(u => u.Recommendation == Models.Entities.ClosingRecommendation.VTBSecondMortgage || u.Recommendation == Models.Entities.ClosingRecommendation.VTBFirstMortgage);
            var atRisk = units.Count(u => u.Recommendation == Models.Entities.ClosingRecommendation.HighRiskDefault || u.Recommendation == Models.Entities.ClosingRecommendation.PotentialDefault);
            var totalSales = units.Sum(u => u.PurchasePrice);
            var totalShortfall = units.Where(u => u.ShortfallAmount > 0).Sum(u => u.ShortfallAmount);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter.Landscape());
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                    // Header
                    page.Header().BorderBottom(2).BorderColor(Colors.Blue.Darken3).PaddingBottom(8).Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("PreConHub AI™").Bold().FontSize(16).FontColor(Colors.Blue.Darken3);
                            col.Item().Text($"{projectName}").Bold().FontSize(12);
                            col.Item().Text(projectAddress).FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(200).AlignRight().Column(col =>
                        {
                            col.Item().Text("Project Closing Report").Bold().FontSize(10);
                            col.Item().Text($"Generated: {DateTime.Now:MMM dd, yyyy h:mm tt}").FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                    });

                    // Content
                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        // Summary Cards Row
                        col.Item().Row(row =>
                        {
                            void MetricBox(IContainer c, string label, string value, string color)
                            {
                                c.Border(1).BorderColor(color).Padding(8).Column(inner =>
                                {
                                    inner.Item().Text(value).Bold().FontSize(16).FontColor(color);
                                    inner.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken2);
                                });
                            }
                            row.RelativeItem().Padding(4, Unit.Point);
                            MetricBox(row.RelativeItem(), "Total Units", totalUnits.ToString(), Colors.Blue.Darken2);
                            row.RelativeItem().Padding(4, Unit.Point);
                            MetricBox(row.RelativeItem(), "Closing Clean", closingClean.ToString(), Colors.Green.Darken1);
                            row.RelativeItem().Padding(4, Unit.Point);
                            MetricBox(row.RelativeItem(), "Need Discount", needDiscount.ToString(), Colors.Blue.Medium);
                            row.RelativeItem().Padding(4, Unit.Point);
                            MetricBox(row.RelativeItem(), "Need VTB", needVTB.ToString(), Colors.Orange.Medium);
                            row.RelativeItem().Padding(4, Unit.Point);
                            MetricBox(row.RelativeItem(), "At Risk", atRisk.ToString(), Colors.Red.Medium);
                            row.RelativeItem().Padding(4, Unit.Point);
                        });

                        col.Item().PaddingTop(5).Row(row =>
                        {
                            row.RelativeItem().Text($"Total Sales: {totalSales:C0}  |  Total Shortfall: {totalShortfall:C0}  |  Capital Recovery: {(totalUnits > 0 ? Math.Round((decimal)closingClean / totalUnits * 100, 1) : 0)}%")
                                .FontSize(9).Bold();
                        });

                        col.Item().PaddingTop(10).Text("Unit Closing Summary").Bold().FontSize(11);

                        // Table
                        col.Item().PaddingTop(5).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(50);   // Unit
                                columns.RelativeColumn(1.2f); // Purchaser
                                columns.RelativeColumn(1);    // Purchase Price
                                columns.RelativeColumn(0.8f); // Mortgage
                                columns.RelativeColumn(0.7f); // Provider
                                columns.RelativeColumn(1);    // SOA Amount
                                columns.RelativeColumn(0.8f); // Deposits
                                columns.RelativeColumn(0.8f); // Shortfall
                                columns.RelativeColumn(0.5f); // %
                                columns.RelativeColumn(1.2f); // Recommendation
                                columns.RelativeColumn(0.7f); // Closing
                            });

                            // Header
                            table.Header(header =>
                            {
                                var headerStyle = TextStyle.Default.FontSize(7).Bold().FontColor(Colors.White);
                                void HeaderCell(IContainer c, string text)
                                {
                                    c.Background(Colors.Blue.Darken3).Padding(4).Text(text).Style(headerStyle);
                                }
                                HeaderCell(header.Cell(), "Unit");
                                HeaderCell(header.Cell(), "Purchaser");
                                HeaderCell(header.Cell(), "Purchase Price");
                                HeaderCell(header.Cell(), "Mortgage");
                                HeaderCell(header.Cell(), "Provider");
                                HeaderCell(header.Cell(), "SOA Amount");
                                HeaderCell(header.Cell(), "Deposits");
                                HeaderCell(header.Cell(), "Shortfall");
                                HeaderCell(header.Cell(), "%");
                                HeaderCell(header.Cell(), "Recommendation");
                                HeaderCell(header.Cell(), "Closing");
                            });

                            // Data rows
                            var sortedUnits = units.OrderByDescending(u => u.ShortfallAmount).ToList();
                            for (int i = 0; i < sortedUnits.Count; i++)
                            {
                                var u = sortedUnits[i];
                                var bgColor = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                                var shortfallColor = u.ShortfallAmount > 0 ? Colors.Red.Darken1 : Colors.Green.Darken1;

                                void DataCell(IContainer c, string text, string? color = null)
                                {
                                    var cell = c.Background(bgColor).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3);
                                    if (color != null)
                                        cell.Text(text).FontSize(7).FontColor(color);
                                    else
                                        cell.Text(text).FontSize(7);
                                }

                                DataCell(table.Cell(), u.UnitNumber);
                                DataCell(table.Cell(), u.PurchaserName ?? "—");
                                DataCell(table.Cell(), u.PurchasePrice.ToString("C0"));
                                DataCell(table.Cell(), u.MortgageAmount.ToString("C0"));
                                DataCell(table.Cell(), u.MortgageProvider ?? "—");
                                DataCell(table.Cell(), u.SOAAmount.ToString("C0"));
                                DataCell(table.Cell(), u.DepositsPaid.ToString("C0"));
                                DataCell(table.Cell(), u.ShortfallAmount > 0 ? $"-{u.ShortfallAmount:C0}" : "$0", shortfallColor);
                                DataCell(table.Cell(), $"{u.ShortfallPercentage:F1}%", shortfallColor);
                                DataCell(table.Cell(), u.RecommendationText, u.StatusColor);
                                DataCell(table.Cell(), u.ClosingDate?.ToString("MMM dd") ?? "—");
                            }
                        });
                    });

                    // Footer
                    page.Footer().BorderTop(1).BorderColor(Colors.Grey.Lighten1).PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Text($"PreConHub AI™ — {projectName}").FontSize(7).FontColor(Colors.Grey.Darken1);
                        row.RelativeItem().AlignCenter().Text("CONFIDENTIAL").FontSize(7).Bold().FontColor(Colors.Red.Darken2);
                        row.RelativeItem().AlignRight().Text(x =>
                        {
                            x.Span("Page ").FontSize(7);
                            x.CurrentPageNumber().FontSize(7);
                            x.Span(" of ").FontSize(7);
                            x.TotalPages().FontSize(7);
                        });
                    });
                });
            });

            using var stream = new MemoryStream();
            document.GeneratePdf(stream);
            return stream.ToArray();
        }

        // ================================================================
        // EXCEL EXPORT - ClosedXML
        // ================================================================
        public byte[] GenerateProjectReportExcel(string projectName, string projectAddress, List<ReportUnitItem> units)
        {
            using var workbook = new XLWorkbook();

            // ── Sheet 1: Summary ──
            var summarySheet = workbook.Worksheets.Add("Summary");
            summarySheet.Cell("A1").Value = $"PreConHub AI™ — {projectName}";
            summarySheet.Cell("A1").Style.Font.Bold = true;
            summarySheet.Cell("A1").Style.Font.FontSize = 14;
            summarySheet.Range("A1:D1").Merge();

            summarySheet.Cell("A2").Value = projectAddress;
            summarySheet.Cell("A2").Style.Font.FontColor = XLColor.Gray;
            summarySheet.Range("A2:D2").Merge();

            summarySheet.Cell("A3").Value = $"Generated: {DateTime.Now:MMM dd, yyyy h:mm tt}";
            summarySheet.Range("A3:D3").Merge();

            int row = 5;
            void AddMetric(string label, object value)
            {
                summarySheet.Cell(row, 1).Value = label;
                summarySheet.Cell(row, 1).Style.Font.Bold = true;
                summarySheet.Cell(row, 2).SetValue(value?.ToString() ?? "");
                row++;
            }

            var totalUnits = units.Count;
            var closingClean = units.Count(u => u.Recommendation == Models.Entities.ClosingRecommendation.ProceedToClose);
            var needDiscount = units.Count(u => u.Recommendation == Models.Entities.ClosingRecommendation.CloseWithDiscount);

            AddMetric("Total Units", totalUnits);
            AddMetric("Closing Clean", closingClean);
            AddMetric("Need Discount", needDiscount);
            AddMetric("Need VTB", units.Count(u => u.Recommendation == Models.Entities.ClosingRecommendation.VTBSecondMortgage || u.Recommendation == Models.Entities.ClosingRecommendation.VTBFirstMortgage));
            AddMetric("At Risk", units.Count(u => u.Recommendation == Models.Entities.ClosingRecommendation.HighRiskDefault || u.Recommendation == Models.Entities.ClosingRecommendation.PotentialDefault));
            AddMetric("Pending", units.Count(u => u.Recommendation == null));
            row++;
            AddMetric("Total Sales", units.Sum(u => u.PurchasePrice).ToString("C0"));
            AddMetric("Total Deposits Paid", units.Sum(u => u.DepositsPaid).ToString("C0"));
            AddMetric("Total Shortfall", units.Where(u => u.ShortfallAmount > 0).Sum(u => u.ShortfallAmount).ToString("C0"));
            AddMetric("Capital Recovery %", totalUnits > 0 ? $"{Math.Round((decimal)closingClean / totalUnits * 100, 1)}%" : "0%");

            summarySheet.Columns().AdjustToContents();

            // ── Sheet 2: Unit Details ──
            var detailSheet = workbook.Worksheets.Add("Unit Details");
            var headers = new[] { "Unit", "Floor", "Type", "Purchaser", "Purchase Price", "Appraisal",
                "Mortgage Provider", "Mortgage Amount", "Approval Type", "SOA Amount", "Deposits Paid",
                "Balance Due", "Cash Required", "Shortfall", "Shortfall %", "Recommendation", "Closing Date" };

            for (int c = 0; c < headers.Length; c++)
            {
                detailSheet.Cell(1, c + 1).Value = headers[c];
                detailSheet.Cell(1, c + 1).Style.Font.Bold = true;
                detailSheet.Cell(1, c + 1).Style.Font.FontColor = XLColor.White;
                detailSheet.Cell(1, c + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1a1a2e");
            }

            var sorted = units.OrderByDescending(u => u.ShortfallAmount).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                var u = sorted[i];
                int r = i + 2;

                detailSheet.Cell(r, 1).Value = u.UnitNumber;
                detailSheet.Cell(r, 2).Value = u.FloorNumber ?? "";
                detailSheet.Cell(r, 3).Value = u.UnitType.ToString();
                detailSheet.Cell(r, 4).Value = u.PurchaserName ?? "";
                detailSheet.Cell(r, 5).Value = u.PurchasePrice;
                detailSheet.Cell(r, 5).Style.NumberFormat.Format = "$#,##0";
                detailSheet.Cell(r, 6).Value = u.CurrentAppraisalValue ?? 0;
                detailSheet.Cell(r, 6).Style.NumberFormat.Format = "$#,##0";
                detailSheet.Cell(r, 7).Value = u.MortgageProvider ?? "";
                detailSheet.Cell(r, 8).Value = u.MortgageAmount;
                detailSheet.Cell(r, 8).Style.NumberFormat.Format = "$#,##0";
                detailSheet.Cell(r, 9).Value = u.MortgageStatusText;
                detailSheet.Cell(r, 10).Value = u.SOAAmount;
                detailSheet.Cell(r, 10).Style.NumberFormat.Format = "$#,##0";
                detailSheet.Cell(r, 11).Value = u.DepositsPaid;
                detailSheet.Cell(r, 11).Style.NumberFormat.Format = "$#,##0";
                detailSheet.Cell(r, 12).Value = u.BalanceDueOnClosing;
                detailSheet.Cell(r, 12).Style.NumberFormat.Format = "$#,##0";
                detailSheet.Cell(r, 13).Value = u.CashRequiredToClose;
                detailSheet.Cell(r, 13).Style.NumberFormat.Format = "$#,##0";
                detailSheet.Cell(r, 14).Value = u.ShortfallAmount;
                detailSheet.Cell(r, 14).Style.NumberFormat.Format = "$#,##0";
                detailSheet.Cell(r, 15).Value = u.ShortfallPercentage / 100;
                detailSheet.Cell(r, 15).Style.NumberFormat.Format = "0.0%";
                detailSheet.Cell(r, 16).Value = u.RecommendationText;
                detailSheet.Cell(r, 17).Value = u.ClosingDate?.ToString("yyyy-MM-dd") ?? "";

                // Color code row by status
                var bgColor = u.Recommendation switch
                {
                    Models.Entities.ClosingRecommendation.ProceedToClose => XLColor.FromHtml("#d4edda"),
                    Models.Entities.ClosingRecommendation.CloseWithDiscount => XLColor.FromHtml("#cce5ff"),
                    Models.Entities.ClosingRecommendation.VTBSecondMortgage => XLColor.FromHtml("#fff3cd"),
                    Models.Entities.ClosingRecommendation.VTBFirstMortgage => XLColor.FromHtml("#ffe4b5"),
                    Models.Entities.ClosingRecommendation.HighRiskDefault or Models.Entities.ClosingRecommendation.PotentialDefault => XLColor.FromHtml("#f8d7da"),
                    _ => XLColor.White
                };

                for (int c = 1; c <= headers.Length; c++)
                    detailSheet.Cell(r, c).Style.Fill.BackgroundColor = bgColor;
            }

            detailSheet.SheetView.FreezeRows(1);
            detailSheet.RangeUsed()?.SetAutoFilter();
            detailSheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        // ================================================================
        // CSV EXPORT - CsvHelper
        // ================================================================
        public byte[] GenerateProjectReportCSV(List<ReportUnitItem> units)
        {
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            csv.WriteField("Unit");
            csv.WriteField("Floor");
            csv.WriteField("Type");
            csv.WriteField("Purchaser");
            csv.WriteField("PurchasePrice");
            csv.WriteField("AppraisalValue");
            csv.WriteField("MortgageProvider");
            csv.WriteField("MortgageAmount");
            csv.WriteField("ApprovalType");
            csv.WriteField("SOAAmount");
            csv.WriteField("DepositsPaid");
            csv.WriteField("BalanceDue");
            csv.WriteField("CashRequired");
            csv.WriteField("Shortfall");
            csv.WriteField("ShortfallPct");
            csv.WriteField("Recommendation");
            csv.WriteField("ClosingDate");
            csv.NextRecord();

            foreach (var u in units.OrderByDescending(u => u.ShortfallAmount))
            {
                csv.WriteField(u.UnitNumber);
                csv.WriteField(u.FloorNumber ?? "");
                csv.WriteField(u.UnitType.ToString());
                csv.WriteField(u.PurchaserName ?? "");
                csv.WriteField(u.PurchasePrice);
                csv.WriteField(u.CurrentAppraisalValue ?? 0);
                csv.WriteField(u.MortgageProvider ?? "");
                csv.WriteField(u.MortgageAmount);
                csv.WriteField(u.MortgageStatusText);
                csv.WriteField(u.SOAAmount);
                csv.WriteField(u.DepositsPaid);
                csv.WriteField(u.BalanceDueOnClosing);
                csv.WriteField(u.CashRequiredToClose);
                csv.WriteField(u.ShortfallAmount);
                csv.WriteField(u.ShortfallPercentage);
                csv.WriteField(u.RecommendationText);
                csv.WriteField(u.ClosingDate?.ToString("yyyy-MM-dd") ?? "");
                csv.NextRecord();
            }

            writer.Flush();
            return stream.ToArray();
        }
    }
}
