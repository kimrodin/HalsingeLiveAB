using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booking.data.Migrations
{
    /// <inheritdoc />
    public partial class PaymentRequestOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentRequestOutboxMessages",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeliveredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SuppressedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentRequestOutboxMessages", x => x.MessageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequestOutboxMessages_BookingId",
                table: "PaymentRequestOutboxMessages",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequestOutboxMessages_DeliveredAtUtc_SuppressedAtUtc~",
                table: "PaymentRequestOutboxMessages",
                columns: new[] { "DeliveredAtUtc", "SuppressedAtUtc", "NextAttemptAtUtc" });

            // Carry unresolved intents from the HTTP implementation into the durable
            // request flow. Existing attempts are reused by BookingId in PaymentSimulator;
            // invalid reservations are suppressed and reconciled without a new charge.
            migrationBuilder.Sql("""
                INSERT INTO "PaymentRequestOutboxMessages"
                    ("MessageId", "BookingId", "Amount", "Currency", "CreatedAtUtc", "NextAttemptAtUtc", "AttemptCount")
                SELECT gen_random_uuid(), "BookingId", "TotalPrice", 'SEK', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, 0
                FROM "Bookings"
                WHERE "PaymentRequested" AND NOT "PaymentSettled" AND "Status" <> 'Confirmed'
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentRequestOutboxMessages");
        }
    }
}
