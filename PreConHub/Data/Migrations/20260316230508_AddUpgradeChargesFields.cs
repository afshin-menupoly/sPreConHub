using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreConHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUpgradeChargesFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UpgradeAmount",
                table: "Units",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpgradePaidDate",
                table: "Units",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FeeType",
                table: "UnitFees",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpgradeAmount",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "UpgradePaidDate",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "FeeType",
                table: "UnitFees");
        }
    }
}
