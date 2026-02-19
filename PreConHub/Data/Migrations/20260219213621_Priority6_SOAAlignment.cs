using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PreConHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class Priority6_SOAAlignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ActualAnnualLandTax",
                table: "Units",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ActualMonthlyMaintenanceFee",
                table: "Units",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ElectronicRegFee",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HCRAFee",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "InterestOnDepositInterest",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OccupancyFeesChargeable",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OccupancyFeesPaid",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SecurityDepositRefund",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "StatusCertFee",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPurchaserCredits",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalVendorCredits",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TransactionLevyFee",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "DepositInterestPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepositId = table.Column<int>(type: "int", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AnnualRate = table.Column<decimal>(type: "decimal(6,3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepositInterestPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepositInterestPeriods_Deposits_DepositId",
                        column: x => x.DepositId,
                        principalTable: "Deposits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemFeeConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HSTApplicable = table.Column<bool>(type: "bit", nullable: false),
                    HSTIncluded = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemFeeConfigs", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "SystemFeeConfigs",
                columns: new[] { "Id", "Amount", "DisplayName", "HSTApplicable", "HSTIncluded", "Key", "Notes", "UpdatedAt", "UpdatedByUserId" },
                values: new object[,]
                {
                    { 1, 170.00m, "HCRA Regulatory Oversight Fee", true, false, "HCRA", "Per unit. HST (13%) added on top at closing. Source: hcraontario.ca. Was $145 at HCRA launch (Feb 2021), increased to $170. Review annually for updates.", new DateTime(2026, 2, 19, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 2, 85.00m, "Electronic Registration Fee (Teranet)", false, true, "ElectronicReg", "Per instrument registered. Statutory fee + ELRSA fee + HST all included in flat rate. CPI-adjusted annually by Ontario government (last updated Nov 3, 2025). Source: teraview.ca.", new DateTime(2026, 2, 19, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 3, 100.00m, "Status Certificate", false, true, "StatusCert", "Regulated maximum under Ontario Condominium Act 1998 s.18(4). Tax-inclusive. Condo corporation must deliver within 10 calendar days of written request + fee payment.", new DateTime(2026, 2, 19, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 4, 65.00m, "Transaction Levy Surcharge (LAWPRO)", true, false, "TransactionLevy", "LAWPRO insurance levy. Base $65 + HST (13%) = ~$73.45 when disbursed to client. Flat province-wide rate. Source: lawpro.ca / Law Society of Ontario.", new DateTime(2026, 2, 19, 0, 0, 0, 0, DateTimeKind.Utc), null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DepositInterestPeriods_DepositId",
                table: "DepositInterestPeriods",
                column: "DepositId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemFeeConfigs_Key",
                table: "SystemFeeConfigs",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DepositInterestPeriods");

            migrationBuilder.DropTable(
                name: "SystemFeeConfigs");

            migrationBuilder.DropColumn(
                name: "ActualAnnualLandTax",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "ActualMonthlyMaintenanceFee",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "ElectronicRegFee",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "HCRAFee",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "InterestOnDepositInterest",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "OccupancyFeesChargeable",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "OccupancyFeesPaid",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "SecurityDepositRefund",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "StatusCertFee",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "TotalPurchaserCredits",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "TotalVendorCredits",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "TransactionLevyFee",
                table: "StatementsOfAdjustments");
        }
    }
}
