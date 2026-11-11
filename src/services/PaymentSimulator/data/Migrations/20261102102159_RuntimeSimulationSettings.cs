using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentSimulator.Data.Migrations
{
    /// <inheritdoc />
    public partial class RuntimeSimulationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SimulationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DelayMs = table.Column<int>(type: "integer", nullable: false),
                    TimeoutMs = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulationSettings", x => x.Id);
                    table.CheckConstraint("CK_SimulationSettings_Valid", "\"Id\" = 1 AND \"Outcome\" IN ('Succeeded', 'Failed', 'TimedOut') AND \"DelayMs\" >= 0 AND \"TimeoutMs\" BETWEEN 1 AND 30000");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SimulationSettings");
        }
    }
}
