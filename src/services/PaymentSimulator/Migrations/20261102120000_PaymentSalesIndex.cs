using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PaymentSimulator.Data;

namespace PaymentSimulator.Migrations;

[DbContext(typeof(PaymentDbContext))]
[Migration("20261102120000_PaymentSalesIndex")]
public sealed class PaymentSalesIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.CreateIndex(
        name: "IX_Payments_PaidAt_Id", table: "Payments", columns: ["PaidAt", "Id"], filter: "\"PaidAt\" IS NOT NULL");
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropIndex(
        name: "IX_Payments_PaidAt_Id", table: "Payments");
}
