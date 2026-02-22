using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreConHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNetSalePriceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AdditionalConsideration",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CommonExpensesFirstMonth",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FederalHST",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetSalePrice",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ProvincialHST",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReserveFundContribution",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SalePrice",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalSalePrice",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdditionalConsideration",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "CommonExpensesFirstMonth",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "FederalHST",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "NetSalePrice",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "ProvincialHST",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "ReserveFundContribution",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "SalePrice",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "TotalSalePrice",
                table: "StatementsOfAdjustments");
        }
    }
}
