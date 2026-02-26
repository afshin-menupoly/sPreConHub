using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreConHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBuyerLawyerFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LawyerAssignments_UnitId_LawyerId",
                table: "LawyerAssignments");

            migrationBuilder.AddColumn<bool>(
                name: "BuyerLawyerConfirmed",
                table: "Units",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "BuyerLawyerConfirmedAt",
                table: "Units",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BuyerLawyerConfirmedAt",
                table: "StatementsOfAdjustments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuyerLawyerNotes",
                table: "StatementsOfAdjustments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmedByBuyerLawyerId",
                table: "StatementsOfAdjustments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsConfirmedByBuyerLawyer",
                table: "StatementsOfAdjustments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_LawyerAssignments_UnitId_LawyerId_Role",
                table: "LawyerAssignments",
                columns: new[] { "UnitId", "LawyerId", "Role" },
                unique: true,
                filter: "[UnitId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LawyerAssignments_UnitId_LawyerId_Role",
                table: "LawyerAssignments");

            migrationBuilder.DropColumn(
                name: "BuyerLawyerConfirmed",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "BuyerLawyerConfirmedAt",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "BuyerLawyerConfirmedAt",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "BuyerLawyerNotes",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "ConfirmedByBuyerLawyerId",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "IsConfirmedByBuyerLawyer",
                table: "StatementsOfAdjustments");

            migrationBuilder.CreateIndex(
                name: "IX_LawyerAssignments_UnitId_LawyerId",
                table: "LawyerAssignments",
                columns: new[] { "UnitId", "LawyerId" },
                unique: true,
                filter: "[UnitId] IS NOT NULL");
        }
    }
}
