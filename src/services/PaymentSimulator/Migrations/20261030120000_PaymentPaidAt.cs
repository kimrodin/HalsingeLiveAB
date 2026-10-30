using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PaymentSimulator.Data;

#nullable disable

namespace PaymentSimulator.Migrations;

[DbContext(typeof(PaymentDbContext))]
[Migration("20261030120000_PaymentPaidAt")]
public partial class PaymentPaidAt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "PaidAt",
            table: "Payments",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql("UPDATE \"Payments\" SET \"PaidAt\" = \"CompletedAt\" WHERE \"Status\" = 'Succeeded'");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "PaidAt", table: "Payments");
    }
}
