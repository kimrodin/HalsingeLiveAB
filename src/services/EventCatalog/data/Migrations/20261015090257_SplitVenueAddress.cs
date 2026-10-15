using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventCatalog.data.Migrations
{
    /// <inheritdoc />
    public partial class SplitVenueAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "VenueAddress",
                table: "Venues",
                newName: "VenueStreetAddress");

            migrationBuilder.AddColumn<string>(
                name: "VenueCity",
                table: "Venues",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VenuePostalCode",
                table: "Venues",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VenueCity",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "VenuePostalCode",
                table: "Venues");

            migrationBuilder.RenameColumn(
                name: "VenueStreetAddress",
                table: "Venues",
                newName: "VenueAddress");
        }
    }
}
