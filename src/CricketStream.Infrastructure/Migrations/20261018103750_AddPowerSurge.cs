using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CricketStream.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPowerSurge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "power_surge_enabled",
                table: "match_rules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "power_surge_from_over",
                table: "match_rules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "power_surge_overs",
                table: "match_rules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "power_surge_to_over",
                table: "match_rules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "power_surge_start_over",
                table: "innings",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "power_surge_enabled",
                table: "match_rules");

            migrationBuilder.DropColumn(
                name: "power_surge_from_over",
                table: "match_rules");

            migrationBuilder.DropColumn(
                name: "power_surge_overs",
                table: "match_rules");

            migrationBuilder.DropColumn(
                name: "power_surge_to_over",
                table: "match_rules");

            migrationBuilder.DropColumn(
                name: "power_surge_start_over",
                table: "innings");
        }
    }
}
