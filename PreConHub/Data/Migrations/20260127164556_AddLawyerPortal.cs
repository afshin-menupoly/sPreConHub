using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreConHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLawyerPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LawyerConfirmed",
                table: "Units",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LawyerConfirmedAt",
                table: "Units",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewStatus",
                table: "LawyerAssignments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "LawyerAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnitId",
                table: "LawyerAssignments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "LawyerAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LawyerNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LawyerAssignmentId = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NoteType = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LawyerNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LawyerNotes_LawyerAssignments_LawyerAssignmentId",
                        column: x => x.LawyerAssignmentId,
                        principalTable: "LawyerAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LawyerAssignments_UnitId",
                table: "LawyerAssignments",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_LawyerNotes_LawyerAssignmentId",
                table: "LawyerNotes",
                column: "LawyerAssignmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_LawyerAssignments_Units_UnitId",
                table: "LawyerAssignments",
                column: "UnitId",
                principalTable: "Units",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LawyerAssignments_Units_UnitId",
                table: "LawyerAssignments");

            migrationBuilder.DropTable(
                name: "LawyerNotes");

            migrationBuilder.DropIndex(
                name: "IX_LawyerAssignments_UnitId",
                table: "LawyerAssignments");

            migrationBuilder.DropColumn(
                name: "LawyerConfirmed",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "LawyerConfirmedAt",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "ReviewStatus",
                table: "LawyerAssignments");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "LawyerAssignments");

            migrationBuilder.DropColumn(
                name: "UnitId",
                table: "LawyerAssignments");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "LawyerAssignments");
        }
    }
}
