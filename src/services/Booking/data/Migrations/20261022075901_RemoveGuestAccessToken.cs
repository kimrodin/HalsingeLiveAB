using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booking.data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGuestAccessToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessTokenHash",
                table: "Bookings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "AccessTokenHash",
                table: "Bookings",
                type: "bytea",
                maxLength: 32,
                nullable: true);
        }
    }
}
