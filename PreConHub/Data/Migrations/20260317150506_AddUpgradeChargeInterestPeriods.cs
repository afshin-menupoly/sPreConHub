using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreConHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUpgradeChargeInterestPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UpgradeChargeInterest",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "UpgradeChargeInterestPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AnnualRate = table.Column<decimal>(type: "decimal(6,3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpgradeChargeInterestPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UpgradeChargeInterestPeriods_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UpgradeChargeInterestPeriods_UnitId",
                table: "UpgradeChargeInterestPeriods",
                column: "UnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UpgradeChargeInterestPeriods");

            migrationBuilder.DropColumn(
                name: "UpgradeChargeInterest",
                table: "StatementsOfAdjustments");
        }
    }
}
