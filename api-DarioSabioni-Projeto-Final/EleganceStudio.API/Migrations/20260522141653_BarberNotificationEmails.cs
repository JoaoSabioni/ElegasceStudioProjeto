using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EleganceStudio.API.Migrations
{
    /// <inheritdoc />
    public partial class BarberNotificationEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Barbers",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Barbers",
                keyColumn: "Id",
                keyValue: new Guid("a1a1a1a1-0000-0000-0000-000000000001"),
                column: "Email",
                value: "t82704366@gmail.com");

            migrationBuilder.UpdateData(
                table: "Barbers",
                keyColumn: "Id",
                keyValue: new Guid("a1a1a1a1-0000-0000-0000-000000000002"),
                column: "Email",
                value: "t82704366@gmail.com");

            migrationBuilder.UpdateData(
                table: "Barbers",
                keyColumn: "Id",
                keyValue: new Guid("a1a1a1a1-0000-0000-0000-000000000003"),
                column: "Email",
                value: "t82704366@gmail.com");

            migrationBuilder.CreateIndex(
                name: "IX_Barbers_Email",
                table: "Barbers",
                column: "Email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Barbers_Email",
                table: "Barbers");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Barbers");
        }
    }
}
