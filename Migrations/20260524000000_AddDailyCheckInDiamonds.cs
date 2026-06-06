using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DACS_Nhóm3.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyCheckInDiamonds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Diamonds",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CheckInStreak",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCheckInDate",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Diamonds",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CheckInStreak",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastCheckInDate",
                table: "AspNetUsers");
        }
    }
}
