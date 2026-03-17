using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreConHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class P1_ParkingLockerCommonExpense_OccupancyFeeAdj : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LockerMonthlyCommonExpense",
                table: "Units",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ParkingMonthlyCommonExpense",
                table: "Units",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OccupancyFeeClosingMonthAdj",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OccupancyFeeTaxRefund",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LockerMonthlyCommonExpense",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "ParkingMonthlyCommonExpense",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "OccupancyFeeClosingMonthAdj",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "OccupancyFeeTaxRefund",
                table: "StatementsOfAdjustments");
        }
    }
}
