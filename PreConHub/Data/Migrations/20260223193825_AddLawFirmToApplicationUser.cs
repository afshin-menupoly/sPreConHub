using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreConHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLawFirmToApplicationUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LawFirm",
                table: "AspNetUsers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LawFirm",
                table: "AspNetUsers");
        }
    }
}
