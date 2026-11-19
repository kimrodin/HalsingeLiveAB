using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booking.data.Migrations
{
    /// <inheritdoc />
    public partial class PseudonymizedCustomerLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PseudonymizedCustomerId",
                table: "Bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PseudonymizedCustomerId",
                table: "Bookings",
                column: "PseudonymizedCustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_PseudonymizedCustomerId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PseudonymizedCustomerId",
                table: "Bookings");
        }
    }
}
