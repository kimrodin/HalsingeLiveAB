using Booking.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booking.data.Migrations;

[DbContext(typeof(BookingDbContext))]
[Migration("20261029120000_BackfillBookingItemCurrency")]
public partial class BackfillBookingItemCurrency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE \"BookingItems\" SET \"Currency\" = 'SEK' WHERE \"Currency\" IS NULL OR btrim(\"Currency\") = '';");

        migrationBuilder.AlterColumn<string>(
            name: "Currency",
            table: "BookingItems",
            type: "character varying(3)",
            maxLength: 3,
            nullable: false,
            defaultValue: "SEK",
            oldClrType: typeof(string),
            oldType: "character varying(3)",
            oldMaxLength: 3,
            oldDefaultValue: "");

        migrationBuilder.AddCheckConstraint(
            name: "CK_BookingItems_Currency_Valid",
            table: "BookingItems",
            sql: "\"Currency\" ~ '^[A-Z]{3}$'");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_BookingItems_Currency_Valid",
            table: "BookingItems");

        migrationBuilder.AlterColumn<string>(
            name: "Currency",
            table: "BookingItems",
            type: "character varying(3)",
            maxLength: 3,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "character varying(3)",
            oldMaxLength: 3,
            oldDefaultValue: "SEK");
    }
}
