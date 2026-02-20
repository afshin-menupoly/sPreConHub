using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreConHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class Priority7_SpecAlignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MarketingAgencyUserId",
                table: "Projects",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SOAVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    BalanceDueOnClosing = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalVendorCredits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPurchaserCredits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CashRequiredToClose = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UploadedFilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedByRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SOAVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SOAVersions_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SOAVersions_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_MarketingAgencyUserId",
                table: "Projects",
                column: "MarketingAgencyUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SOAVersions_CreatedAt",
                table: "SOAVersions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SOAVersions_CreatedByUserId",
                table: "SOAVersions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SOAVersions_UnitId_VersionNumber",
                table: "SOAVersions",
                columns: new[] { "UnitId", "VersionNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_AspNetUsers_MarketingAgencyUserId",
                table: "Projects",
                column: "MarketingAgencyUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_AspNetUsers_MarketingAgencyUserId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "SOAVersions");

            migrationBuilder.DropIndex(
                name: "IX_Projects_MarketingAgencyUserId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "MarketingAgencyUserId",
                table: "Projects");
        }
    }
}
