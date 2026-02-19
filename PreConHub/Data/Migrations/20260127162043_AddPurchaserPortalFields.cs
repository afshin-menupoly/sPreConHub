using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreConHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaserPortalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExistingMortgageBalance",
                table: "PurchaserFinancials",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExistingPropertyValue",
                table: "PurchaserFinancials",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedSaleDate",
                table: "PurchaserFinancials",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GiftFromFamily",
                table: "PurchaserFinancials",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "HasExistingPropertyToSell",
                table: "PurchaserFinancials",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPropertyListed",
                table: "PurchaserFinancials",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "OtherFundsAmount",
                table: "PurchaserFinancials",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "OtherFundsDescription",
                table: "PurchaserFinancials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProceedsFromSale",
                table: "PurchaserFinancials",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RRSPAvailable",
                table: "PurchaserFinancials",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalFundsAvailable",
                table: "PurchaserFinancials",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Conditions",
                table: "MortgageInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasConditions",
                table: "MortgageInfos",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExistingMortgageBalance",
                table: "PurchaserFinancials");

            migrationBuilder.DropColumn(
                name: "ExistingPropertyValue",
                table: "PurchaserFinancials");

            migrationBuilder.DropColumn(
                name: "ExpectedSaleDate",
                table: "PurchaserFinancials");

            migrationBuilder.DropColumn(
                name: "GiftFromFamily",
                table: "PurchaserFinancials");

            migrationBuilder.DropColumn(
                name: "HasExistingPropertyToSell",
                table: "PurchaserFinancials");

            migrationBuilder.DropColumn(
                name: "IsPropertyListed",
                table: "PurchaserFinancials");

            migrationBuilder.DropColumn(
                name: "OtherFundsAmount",
                table: "PurchaserFinancials");

            migrationBuilder.DropColumn(
                name: "OtherFundsDescription",
                table: "PurchaserFinancials");

            migrationBuilder.DropColumn(
                name: "ProceedsFromSale",
                table: "PurchaserFinancials");

            migrationBuilder.DropColumn(
                name: "RRSPAvailable",
                table: "PurchaserFinancials");

            migrationBuilder.DropColumn(
                name: "TotalFundsAvailable",
                table: "PurchaserFinancials");

            migrationBuilder.DropColumn(
                name: "Conditions",
                table: "MortgageInfos");

            migrationBuilder.DropColumn(
                name: "HasConditions",
                table: "MortgageInfos");
        }
    }
}
