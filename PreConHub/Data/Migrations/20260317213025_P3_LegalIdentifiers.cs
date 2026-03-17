using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreConHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class P3_LegalIdentifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DwellingUnitNumber",
                table: "Units",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LockerUnitNumber",
                table: "Units",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParkingUnitNumber",
                table: "Units",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TarionUnitEnrolmentNumber",
                table: "Units",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BuilderHSTNumber",
                table: "Projects",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CondoCorpNumber",
                table: "Projects",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DwellingUnitNumber",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "LockerUnitNumber",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "ParkingUnitNumber",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "TarionUnitEnrolmentNumber",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "BuilderHSTNumber",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "CondoCorpNumber",
                table: "Projects");
        }
    }
}
