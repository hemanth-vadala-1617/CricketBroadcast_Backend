using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CricketStream.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchBreaks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "break_kind",
                table: "matches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "break_planned_seconds",
                table: "matches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "break_started_at",
                table: "matches",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "break_kind",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "break_planned_seconds",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "break_started_at",
                table: "matches");
        }
    }
}
