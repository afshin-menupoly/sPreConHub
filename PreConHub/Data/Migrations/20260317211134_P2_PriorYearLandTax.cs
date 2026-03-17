using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreConHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class P2_PriorYearLandTax : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PriorYearAnnualLandTax",
                table: "Units",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PriorYearAnnualLandTax",
                table: "Units");
        }
    }
}
