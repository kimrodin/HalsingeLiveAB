using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventCatalog.data.Migrations
{
    /// <inheritdoc />
    public partial class AlignVenueAndSectionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SeatingType",
                table: "Venues");

            migrationBuilder.AlterColumn<int>(
                name: "SectionCapacity",
                table: "Sections",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SeatingType",
                table: "Venues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "SectionCapacity",
                table: "Sections",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);
        }
    }
}
