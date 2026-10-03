using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CricketStream.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddThemeUseTeamColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "use_team_colors",
                table: "overlay_themes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "use_team_colors",
                table: "overlay_themes");
        }
    }
}
