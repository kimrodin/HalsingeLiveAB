using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PaymentSimulator.Data;

#nullable disable

namespace PaymentSimulator.Migrations;

[DbContext(typeof(PaymentDbContext))]
[Migration("20261022140000_DurablePaymentProcessing")]
public partial class DurablePaymentProcessing : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(name: "CompleteAfter", table: "Payments",
            type: "timestamp with time zone", nullable: false, defaultValue: DateTimeOffset.UnixEpoch);
        migrationBuilder.AddColumn<string>(name: "PlannedOutcome", table: "Payments",
            type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "TimedOut");
        migrationBuilder.AddColumn<DateTimeOffset>(name: "RefundedAt", table: "Payments",
            type: "timestamp with time zone", nullable: true);
        // Legacy Pending rows lack a durable planned result. Recover them as TimedOut;
        // never invent a successful charge during an upgrade.
        migrationBuilder.CreateIndex(name: "IX_Payments_Status_CompleteAfter", table: "Payments",
            columns: new[] { "Status", "CompleteAfter" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Payments_Status_CompleteAfter", table: "Payments");
        migrationBuilder.DropColumn(name: "CompleteAfter", table: "Payments");
        migrationBuilder.DropColumn(name: "PlannedOutcome", table: "Payments");
        migrationBuilder.DropColumn(name: "RefundedAt", table: "Payments");
    }
}
