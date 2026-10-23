using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booking.data.Migrations
{
    public partial class PaymentReconciliation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PaymentRequested",
                table: "Bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PaymentSettled",
                table: "Bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PaymentRequested", table: "Bookings");
            migrationBuilder.DropColumn(name: "PaymentSettled", table: "Bookings");
        }
    }
}
