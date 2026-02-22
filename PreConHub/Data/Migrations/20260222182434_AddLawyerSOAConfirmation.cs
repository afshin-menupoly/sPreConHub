using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreConHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLawyerSOAConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLawyerSOAConfirmed",
                table: "StatementsOfAdjustments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LawyerSOAConfirmedAt",
                table: "StatementsOfAdjustments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LawyerSOAConfirmedByRole",
                table: "StatementsOfAdjustments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LawyerSOAConfirmedByUserId",
                table: "StatementsOfAdjustments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SystemBalanceDueOnClosing",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SystemCashRequiredToClose",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLawyerSOAConfirmed",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "LawyerSOAConfirmedAt",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "LawyerSOAConfirmedByRole",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "LawyerSOAConfirmedByUserId",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "SystemBalanceDueOnClosing",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "SystemCashRequiredToClose",
                table: "StatementsOfAdjustments");
        }
    }
}
