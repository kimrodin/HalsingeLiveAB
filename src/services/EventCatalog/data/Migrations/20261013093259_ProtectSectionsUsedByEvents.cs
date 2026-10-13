using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventCatalog.data.Migrations
{
    /// <inheritdoc />
    public partial class ProtectSectionsUsedByEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EventSectionPrice_Sections_SectionId",
                table: "EventSectionPrice");

            migrationBuilder.AddForeignKey(
                name: "FK_EventSectionPrice_Sections_SectionId",
                table: "EventSectionPrice",
                column: "SectionId",
                principalTable: "Sections",
                principalColumn: "SectionId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EventSectionPrice_Sections_SectionId",
                table: "EventSectionPrice");

            migrationBuilder.AddForeignKey(
                name: "FK_EventSectionPrice_Sections_SectionId",
                table: "EventSectionPrice",
                column: "SectionId",
                principalTable: "Sections",
                principalColumn: "SectionId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
