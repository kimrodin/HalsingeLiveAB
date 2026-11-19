using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booking.data.Migrations
{
    /// <inheritdoc />
    public partial class PersonalDataPseudonymization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PersonalDataPseudonymizedAtUtc",
                table: "Bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CreatedAt_PersonalDataPseudonymizedAtUtc",
                table: "Bookings",
                columns: new[] { "CreatedAt", "PersonalDataPseudonymizedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_CreatedAt_PersonalDataPseudonymizedAtUtc",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PersonalDataPseudonymizedAtUtc",
                table: "Bookings");
        }
    }
}
