using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CricketStream.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerCareerStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "career_average",
                table: "players",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "career_fifties",
                table: "players",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "career_hundreds",
                table: "players",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "career_label",
                table: "players",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "career_matches",
                table: "players",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "career_runs",
                table: "players",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "career_strike_rate",
                table: "players",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "career_average",
                table: "players");

            migrationBuilder.DropColumn(
                name: "career_fifties",
                table: "players");

            migrationBuilder.DropColumn(
                name: "career_hundreds",
                table: "players");

            migrationBuilder.DropColumn(
                name: "career_label",
                table: "players");

            migrationBuilder.DropColumn(
                name: "career_matches",
                table: "players");

            migrationBuilder.DropColumn(
                name: "career_runs",
                table: "players");

            migrationBuilder.DropColumn(
                name: "career_strike_rate",
                table: "players");
        }
    }
}
