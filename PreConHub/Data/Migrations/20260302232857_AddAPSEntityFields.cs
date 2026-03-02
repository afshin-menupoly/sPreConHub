using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PreConHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAPSEntityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DelayedOccupancyDate",
                table: "Units",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstTentativeOccupancyDate",
                table: "Units",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LockerCount",
                table: "Units",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "OutsideOccupancyDate",
                table: "Units",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParkingCount",
                table: "Units",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PurchaserTerminationDate",
                table: "Units",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CommencementOfConstructionDate",
                table: "Projects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PropertyLegalDescription",
                table: "Projects",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TarionRegistrationNumber",
                table: "Projects",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VendorSolicitorAddress",
                table: "Projects",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VendorSolicitorEmail",
                table: "Projects",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VendorSolicitorName",
                table: "Projects",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VendorSolicitorPhone",
                table: "Projects",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DepositPercentage",
                table: "Deposits",
                type: "decimal(5,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DelayedOccupancyDate",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "FirstTentativeOccupancyDate",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "LockerCount",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "OutsideOccupancyDate",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "ParkingCount",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "PurchaserTerminationDate",
                table: "Units");

            migrationBuilder.DropColumn(
                name: "CommencementOfConstructionDate",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "PropertyLegalDescription",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "TarionRegistrationNumber",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "VendorSolicitorAddress",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "VendorSolicitorEmail",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "VendorSolicitorName",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "VendorSolicitorPhone",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DepositPercentage",
                table: "Deposits");
        }
    }
}
