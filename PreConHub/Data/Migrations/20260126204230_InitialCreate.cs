using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreConHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "UserType",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PostalCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ProjectType = table.Column<int>(type: "int", nullable: false),
                    TotalUnits = table.Column<int>(type: "int", nullable: false),
                    OccupancyDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BuilderId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_AspNetUsers_BuilderId",
                        column: x => x.BuilderId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LawyerAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    LawyerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LawyerAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LawyerAssignments_AspNetUsers_LawyerId",
                        column: x => x.LawyerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LawyerAssignments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectFees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    FeeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FeeType = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsPercentage = table.Column<bool>(type: "bit", nullable: false),
                    AppliesToAllUnits = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsConfirmedByLawyer = table.Column<bool>(type: "bit", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedByLawyerId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectFees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectFees_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectSummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    TotalUnits = table.Column<int>(type: "int", nullable: false),
                    UnitsReadyToClose = table.Column<int>(type: "int", nullable: false),
                    UnitsNeedingDiscount = table.Column<int>(type: "int", nullable: false),
                    UnitsNeedingVTB = table.Column<int>(type: "int", nullable: false),
                    UnitsAtRisk = table.Column<int>(type: "int", nullable: false),
                    UnitsPendingData = table.Column<int>(type: "int", nullable: false),
                    PercentReadyToClose = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    PercentNeedingDiscount = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    PercentNeedingVTB = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    PercentAtRisk = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TotalSalesValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDiscountRequired = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountPercentOfSales = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TotalInvestmentAtRisk = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalShortfall = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ClosingProbabilityPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectSummaries_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Units",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    UnitNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FloorNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UnitType = table.Column<int>(type: "int", nullable: false),
                    Bedrooms = table.Column<int>(type: "int", nullable: false),
                    Bathrooms = table.Column<int>(type: "int", nullable: false),
                    SquareFootage = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentAppraisalValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AppraisalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HasParking = table.Column<bool>(type: "bit", nullable: false),
                    ParkingPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HasLocker = table.Column<bool>(type: "bit", nullable: false),
                    LockerPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OccupancyDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirmClosingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Recommendation = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsConfirmedByLawyer = table.Column<bool>(type: "bit", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedByLawyerId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Units", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Units_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Deposits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    DepositName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsConfirmedByLawyer = table.Column<bool>(type: "bit", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedByLawyerId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deposits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Deposits_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: true),
                    UnitId = table.Column<int>(type: "int", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    DocumentType = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UploadedById = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GoogleDriveFileId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GoogleDriveUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_AspNetUsers_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Documents_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Documents_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OccupancyFees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InterestComponent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PropertyTaxComponent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CommonExpenseComponent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalMonthlyFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    PaidDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OccupancyFees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OccupancyFees_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShortfallAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    SOAAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MortgageApproved = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DepositsPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AdditionalCashAvailable = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalFundsAvailable = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ShortfallAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ShortfallPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    RiskLevel = table.Column<int>(type: "int", nullable: false),
                    Recommendation = table.Column<int>(type: "int", nullable: false),
                    SuggestedDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SuggestedVTBAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AIAnalysis = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RecommendationReasoning = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShortfallAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShortfallAnalyses_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StatementsOfAdjustments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LandTransferTax = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TorontoLandTransferTax = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DevelopmentCharges = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TarionFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UtilityConnectionFees = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PropertyTaxAdjustment = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CommonExpenseAdjustment = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OccupancyFeesOwing = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ParkingPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LockerPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Upgrades = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LegalFeesEstimate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OtherDebits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDebits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DepositsPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DepositInterest = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BuilderCredits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OtherCredits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCredits = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceDueOnClosing = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MortgageAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CashRequiredToClose = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CalculationVersion = table.Column<int>(type: "int", nullable: false),
                    IsConfirmedByLawyer = table.Column<bool>(type: "bit", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedByLawyerId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LawyerNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatementsOfAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatementsOfAdjustments_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UnitFees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    FeeName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsCredit = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitFees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitFees_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UnitPurchasers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitId = table.Column<int>(type: "int", nullable: false),
                    PurchaserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsPrimaryPurchaser = table.Column<bool>(type: "bit", nullable: false),
                    OwnershipPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitPurchasers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitPurchasers_AspNetUsers_PurchaserId",
                        column: x => x.PurchaserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitPurchasers_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MortgageInfos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitPurchaserId = table.Column<int>(type: "int", nullable: false),
                    HasMortgageApproval = table.Column<bool>(type: "bit", nullable: false),
                    ApprovalType = table.Column<int>(type: "int", nullable: false),
                    MortgageProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    InterestRate = table.Column<decimal>(type: "decimal(5,3)", nullable: true),
                    AmortizationYears = table.Column<int>(type: "int", nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsApprovalConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    BuilderCanViewAmount = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MortgageInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MortgageInfos_UnitPurchasers_UnitPurchaserId",
                        column: x => x.UnitPurchaserId,
                        principalTable: "UnitPurchasers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaserFinancials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitPurchaserId = table.Column<int>(type: "int", nullable: false),
                    AnnualIncome = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AdditionalCashAvailable = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OtherAssetsValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IncomeSource = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EmploymentStatus = table.Column<int>(type: "int", nullable: false),
                    Employer = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaserFinancials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaserFinancials_UnitPurchasers_UnitPurchaserId",
                        column: x => x.UnitPurchaserId,
                        principalTable: "UnitPurchasers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Email",
                table: "AspNetUsers",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_UserType",
                table: "AspNetUsers",
                column: "UserType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityId",
                table: "AuditLogs",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType",
                table: "AuditLogs",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Deposits_Status",
                table: "Deposits",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Deposits_UnitId",
                table: "Deposits",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DocumentType",
                table: "Documents",
                column: "DocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ProjectId",
                table: "Documents",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UnitId",
                table: "Documents",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UploadedById",
                table: "Documents",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_LawyerAssignments_LawyerId",
                table: "LawyerAssignments",
                column: "LawyerId");

            migrationBuilder.CreateIndex(
                name: "IX_LawyerAssignments_ProjectId_LawyerId",
                table: "LawyerAssignments",
                columns: new[] { "ProjectId", "LawyerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MortgageInfos_UnitPurchaserId",
                table: "MortgageInfos",
                column: "UnitPurchaserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OccupancyFees_UnitId",
                table: "OccupancyFees",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFees_ProjectId",
                table: "ProjectFees",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_BuilderId",
                table: "Projects",
                column: "BuilderId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_BuilderId_Status",
                table: "Projects",
                columns: new[] { "BuilderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Status",
                table: "Projects",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSummaries_ProjectId",
                table: "ProjectSummaries",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaserFinancials_UnitPurchaserId",
                table: "PurchaserFinancials",
                column: "UnitPurchaserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShortfallAnalyses_Recommendation",
                table: "ShortfallAnalyses",
                column: "Recommendation");

            migrationBuilder.CreateIndex(
                name: "IX_ShortfallAnalyses_RiskLevel",
                table: "ShortfallAnalyses",
                column: "RiskLevel");

            migrationBuilder.CreateIndex(
                name: "IX_ShortfallAnalyses_UnitId",
                table: "ShortfallAnalyses",
                column: "UnitId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatementsOfAdjustments_IsConfirmedByLawyer",
                table: "StatementsOfAdjustments",
                column: "IsConfirmedByLawyer");

            migrationBuilder.CreateIndex(
                name: "IX_StatementsOfAdjustments_UnitId",
                table: "StatementsOfAdjustments",
                column: "UnitId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitFees_UnitId",
                table: "UnitFees",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitPurchasers_PurchaserId",
                table: "UnitPurchasers",
                column: "PurchaserId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitPurchasers_UnitId_PurchaserId",
                table: "UnitPurchasers",
                columns: new[] { "UnitId", "PurchaserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Units_ProjectId",
                table: "Units",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Units_ProjectId_UnitNumber",
                table: "Units",
                columns: new[] { "ProjectId", "UnitNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Units_Recommendation",
                table: "Units",
                column: "Recommendation");

            migrationBuilder.CreateIndex(
                name: "IX_Units_Status",
                table: "Units",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Deposits");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "LawyerAssignments");

            migrationBuilder.DropTable(
                name: "MortgageInfos");

            migrationBuilder.DropTable(
                name: "OccupancyFees");

            migrationBuilder.DropTable(
                name: "ProjectFees");

            migrationBuilder.DropTable(
                name: "ProjectSummaries");

            migrationBuilder.DropTable(
                name: "PurchaserFinancials");

            migrationBuilder.DropTable(
                name: "ShortfallAnalyses");

            migrationBuilder.DropTable(
                name: "StatementsOfAdjustments");

            migrationBuilder.DropTable(
                name: "UnitFees");

            migrationBuilder.DropTable(
                name: "UnitPurchasers");

            migrationBuilder.DropTable(
                name: "Units");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_Email",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_UserType",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastLoginAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UserType",
                table: "AspNetUsers");
        }
    }
}
