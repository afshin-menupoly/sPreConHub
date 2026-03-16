using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreConHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitFeeFeeType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedMonthlyOccupancyFee",
                table: "Units",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LockerNumber",
                table: "Units",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OccupancyFeeEstCommonExpense",
                table: "Units",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OccupancyFeeEstPropertyTax",
                table: "Units",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OccupancyFeeInterestRate",
                table: "Units",
                type: "decimal(5,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParkingNumber",
                table: "Units",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedMonthlyOccupancyFee",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "LockerNumber",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "OccupancyFeeEstCommonExpense",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "OccupancyFeeEstPropertyTax",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "OccupancyFeeInterestRate",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "ParkingNumber",
                table: "Units");
        }
    }
}
