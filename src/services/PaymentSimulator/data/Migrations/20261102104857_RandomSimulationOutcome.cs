using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentSimulator.Data.Migrations
{
    /// <inheritdoc />
    public partial class RandomSimulationOutcome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SimulationSettings_Valid",
                table: "SimulationSettings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SimulationSettings_Valid",
                table: "SimulationSettings",
                sql: "\"Id\" = 1 AND \"Outcome\" IN ('Succeeded', 'Failed', 'TimedOut', 'Random') AND \"DelayMs\" >= 0 AND \"TimeoutMs\" BETWEEN 1 AND 30000");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SimulationSettings_Valid",
                table: "SimulationSettings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SimulationSettings_Valid",
                table: "SimulationSettings",
                sql: "\"Id\" = 1 AND \"Outcome\" IN ('Succeeded', 'Failed', 'TimedOut') AND \"DelayMs\" >= 0 AND \"TimeoutMs\" BETWEEN 1 AND 30000");
        }
    }
}
