using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreConHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLateClosingPenalty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DailyPenaltyAmount",
                table: "Units",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPenaltyActive",
                table: "Units",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PenaltyDaysCount",
                table: "Units",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PenaltyPausedAt",
                table: "Units",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PenaltyStartDate",
                table: "Units",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAccumulatedPenalty",
                table: "Units",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LatePenalties",
                table: "StatementsOfAdjustments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "ClosingPenalties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    PenaltyDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DailyAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AccumulatedTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClosingPenalties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClosingPenalties_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClosingPenalties_UnitId",
                table: "ClosingPenalties",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ClosingPenalties_UnitId_PenaltyDate",
                table: "ClosingPenalties",
                columns: new[] { "UnitId", "PenaltyDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClosingPenalties");

            migrationBuilder.DropColumn(
                name: "DailyPenaltyAmount",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "IsPenaltyActive",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "PenaltyDaysCount",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "PenaltyPausedAt",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "PenaltyStartDate",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "TotalAccumulatedPenalty",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "LatePenalties",
                table: "StatementsOfAdjustments");
        }
    }
}
