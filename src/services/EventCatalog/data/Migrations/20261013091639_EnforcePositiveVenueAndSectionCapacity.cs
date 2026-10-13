using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventCatalog.data.Migrations
{
    /// <inheritdoc />
    public partial class EnforcePositiveVenueAndSectionCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "SectionCapacity",
                table: "Sections",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Venues_PositiveCapacity",
                table: "Venues",
                sql: "\"VenueCapacity\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sections_PositiveCapacity",
                table: "Sections",
                sql: "\"SectionCapacity\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Venues_PositiveCapacity",
                table: "Venues");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sections_PositiveCapacity",
                table: "Sections");

            migrationBuilder.AlterColumn<int>(
                name: "SectionCapacity",
                table: "Sections",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
