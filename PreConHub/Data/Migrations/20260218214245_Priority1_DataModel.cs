using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreConHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class Priority1_DataModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Conditional: index may not exist if DB was set up differently
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LawyerAssignments_ProjectId_LawyerId' AND object_id = OBJECT_ID('LawyerAssignments')) " +
                "DROP INDEX [IX_LawyerAssignments_ProjectId_LawyerId] ON [LawyerAssignments];");

            // Conditional: UnitId non-unique index may not exist
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LawyerAssignments_UnitId' AND object_id = OBJECT_ID('LawyerAssignments')) " +
                "DROP INDEX [IX_LawyerAssignments_UnitId] ON [LawyerAssignments];");

            migrationBuilder.AddColumn<string>(
                name: "BuilderModifiedSuggestion",
                table: "ShortfallAnalyses",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionAction",
                table: "ShortfallAnalyses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DecisionAt",
                table: "ShortfallAnalyses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionByBuilderId",
                table: "ShortfallAnalyses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MutualReleaseThreshold",
                table: "ShortfallAnalyses",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowMarketingAccess",
                table: "Projects",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CreditBureau",
                table: "MortgageInfos",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreditScore",
                table: "MortgageInfos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EstimatedFundingDate",
                table: "MortgageInfos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBlanketMortgage",
                table: "MortgageInfos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchaserAppraisalValue",
                table: "MortgageInfos",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Comments",
                table: "AuditLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClosingExtensionRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    RequestedByPurchaserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RequestedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OriginalClosingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestedNewClosingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewedByBuilderId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewerNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClosingExtensionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClosingExtensionRequests_AspNetUsers_RequestedByPurchaserId",
                        column: x => x.RequestedByPurchaserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClosingExtensionRequests_AspNetUsers_ReviewedByBuilderId",
                        column: x => x.ReviewedByBuilderId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClosingExtensionRequests_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectFinancials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    TotalRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalInvestment = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MarketingCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProfitAvailable = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxBuilderCapital = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectFinancials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectFinancials_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LawyerAssignments_ProjectId",
                table: "LawyerAssignments",
                column: "ProjectId");

            // Conditional: unique index may already exist in DB
            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LawyerAssignments_UnitId_LawyerId' AND object_id = OBJECT_ID('LawyerAssignments')) " +
                "CREATE UNIQUE INDEX [IX_LawyerAssignments_UnitId_LawyerId] ON [LawyerAssignments] ([UnitId], [LawyerId]) WHERE [UnitId] IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_ClosingExtensionRequests_RequestedByPurchaserId",
                table: "ClosingExtensionRequests",
                column: "RequestedByPurchaserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClosingExtensionRequests_ReviewedByBuilderId",
                table: "ClosingExtensionRequests",
                column: "ReviewedByBuilderId");

            migrationBuilder.CreateIndex(
                name: "IX_ClosingExtensionRequests_Status",
                table: "ClosingExtensionRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ClosingExtensionRequests_UnitId",
                table: "ClosingExtensionRequests",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFinancials_ProjectId",
                table: "ProjectFinancials",
                column: "ProjectId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClosingExtensionRequests");

            migrationBuilder.DropTable(
                name: "ProjectFinancials");

            migrationBuilder.DropIndex(
                name: "IX_LawyerAssignments_ProjectId",
                table: "LawyerAssignments");

            migrationBuilder.DropIndex(
                name: "IX_LawyerAssignments_UnitId_LawyerId",
                table: "LawyerAssignments");

            migrationBuilder.DropColumn(
                name: "BuilderModifiedSuggestion",
                table: "ShortfallAnalyses");

            migrationBuilder.DropColumn(
                name: "DecisionAction",
                table: "ShortfallAnalyses");

            migrationBuilder.DropColumn(
                name: "DecisionAt",
                table: "ShortfallAnalyses");

            migrationBuilder.DropColumn(
                name: "DecisionByBuilderId",
                table: "ShortfallAnalyses");

            migrationBuilder.DropColumn(
                name: "MutualReleaseThreshold",
                table: "ShortfallAnalyses");

            migrationBuilder.DropColumn(
                name: "AllowMarketingAccess",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "CreditBureau",
                table: "MortgageInfos");

            migrationBuilder.DropColumn(
                name: "CreditScore",
                table: "MortgageInfos");

            migrationBuilder.DropColumn(
                name: "EstimatedFundingDate",
                table: "MortgageInfos");

            migrationBuilder.DropColumn(
                name: "IsBlanketMortgage",
                table: "MortgageInfos");

            migrationBuilder.DropColumn(
                name: "PurchaserAppraisalValue",
                table: "MortgageInfos");

            migrationBuilder.DropColumn(
                name: "Comments",
                table: "AuditLogs");

            migrationBuilder.CreateIndex(
                name: "IX_LawyerAssignments_ProjectId_LawyerId",
                table: "LawyerAssignments",
                columns: new[] { "ProjectId", "LawyerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LawyerAssignments_UnitId",
                table: "LawyerAssignments",
                column: "UnitId");
        }
    }
}
