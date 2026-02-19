using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreConHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class SOAEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "APSDate",
                table: "Units",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InterimOccupancyStartDate",
                table: "Units",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFirstTimeBuyer",
                table: "Units",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPrimaryResidence",
                table: "Units",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "BuilderAbsorbedLevies",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "BuilderConfirmedAt",
                table: "StatementsOfAdjustments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CashBackIncentives",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CommunityBenefitCharges",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmedByBuilderId",
                table: "StatementsOfAdjustments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DesignCredits",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EducationDevelopmentCharges",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FreeUpgradesValue",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HSTAmount",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HSTRebateFederal",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HSTRebateOntario",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HSTRebateTotal",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsConfirmedByBuilder",
                table: "StatementsOfAdjustments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsHSTRebateAssignedToBuilder",
                table: "StatementsOfAdjustments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsHSTRebateEligible",
                table: "StatementsOfAdjustments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "StatementsOfAdjustments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedAt",
                table: "StatementsOfAdjustments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LockedByUserId",
                table: "StatementsOfAdjustments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NetHSTPayable",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ParklandLevy",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CompoundingType",
                table: "Deposits",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Holder",
                table: "Deposits",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "InterestRate",
                table: "Deposits",
                type: "decimal(5,3)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInterestEligible",
                table: "Deposits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ProjectLevyCap",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    LevyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CapAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExcessResponsibility = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectLevyCap", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectLevyCap_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectLevyCap_ProjectId",
                table: "ProjectLevyCap",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectLevyCap");

            migrationBuilder.DropColumn(
                name: "APSDate",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "InterimOccupancyStartDate",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "IsFirstTimeBuyer",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "IsPrimaryResidence",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "BuilderAbsorbedLevies",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "BuilderConfirmedAt",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "CashBackIncentives",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "CommunityBenefitCharges",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "ConfirmedByBuilderId",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "DesignCredits",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "EducationDevelopmentCharges",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "FreeUpgradesValue",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "HSTAmount",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "HSTRebateFederal",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "HSTRebateOntario",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "HSTRebateTotal",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "IsConfirmedByBuilder",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "IsHSTRebateAssignedToBuilder",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "IsHSTRebateEligible",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "LockedAt",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "LockedByUserId",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "NetHSTPayable",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "ParklandLevy",
                table: "StatementsOfAdjustments");

            migrationBuilder.DropColumn(
                name: "CompoundingType",
                table: "Deposits");

            migrationBuilder.DropColumn(
                name: "Holder",
                table: "Deposits");

            migrationBuilder.DropColumn(
                name: "InterestRate",
                table: "Deposits");

            migrationBuilder.DropColumn(
                name: "IsInterestEligible",
                table: "Deposits");
        }
    }
}
