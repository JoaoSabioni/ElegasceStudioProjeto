using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EleganceStudio.API.Migrations
{
    /// <inheritdoc />
    public partial class EmailNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientEmail",
                table: "Bookings",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ClientEmail",
                table: "BookingLogs",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ClientEmail",
                table: "Bookings",
                column: "ClientEmail");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_ClientEmail",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ClientEmail",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ClientEmail",
                table: "BookingLogs");
        }
    }
}
