using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CricketStream.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    user_name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    password_hash = table.Column<string>(type: "TEXT", nullable: true),
                    security_stamp = table.Column<string>(type: "TEXT", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "TEXT", nullable: true),
                    phone_number = table.Column<string>(type: "TEXT", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    lockout_end = table.Column<long>(type: "INTEGER", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    access_failed_count = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<string>(type: "TEXT", nullable: false),
                    match_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    action_type = table.Column<string>(type: "TEXT", nullable: false),
                    entity_name = table.Column<string>(type: "TEXT", nullable: false),
                    entity_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    previous_state_json = table.Column<string>(type: "TEXT", nullable: false),
                    after_state_json = table.Column<string>(type: "TEXT", nullable: true),
                    reason = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "match_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    is_test = table.Column<bool>(type: "INTEGER", nullable: false),
                    balls_per_over = table.Column<int>(type: "INTEGER", nullable: false),
                    overs_per_innings = table.Column<int>(type: "INTEGER", nullable: false),
                    max_overs_per_bowler = table.Column<int>(type: "INTEGER", nullable: false),
                    powerplay_overs = table.Column<int>(type: "INTEGER", nullable: false),
                    wide_runs = table.Column<int>(type: "INTEGER", nullable: false),
                    no_ball_runs = table.Column<int>(type: "INTEGER", nullable: false),
                    free_hit_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    drs_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    follow_on_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    innings_per_side = table.Column<int>(type: "INTEGER", nullable: false),
                    days = table.Column<int>(type: "INTEGER", nullable: false),
                    overs_per_day = table.Column<int>(type: "INTEGER", nullable: false),
                    sessions_per_day = table.Column<int>(type: "INTEGER", nullable: false),
                    new_ball_after_overs = table.Column<int>(type: "INTEGER", nullable: false),
                    follow_on_margin = table.Column<int>(type: "INTEGER", nullable: false),
                    power_surge_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    power_surge_overs = table.Column<int>(type: "INTEGER", nullable: false),
                    power_surge_from_over = table.Column<int>(type: "INTEGER", nullable: false),
                    power_surge_to_over = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "overlay_themes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    primary_color = table.Column<string>(type: "TEXT", nullable: false),
                    secondary_color = table.Column<string>(type: "TEXT", nullable: false),
                    accent_color = table.Column<string>(type: "TEXT", nullable: false),
                    background_url = table.Column<string>(type: "TEXT", nullable: true),
                    logo_url = table.Column<string>(type: "TEXT", nullable: true),
                    watermark_url = table.Column<string>(type: "TEXT", nullable: true),
                    watermark_text = table.Column<string>(type: "TEXT", nullable: true),
                    use_team_colors = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_overlay_themes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<string>(type: "TEXT", nullable: false),
                    token = table.Column<string>(type: "TEXT", nullable: false),
                    expires_at = table.Column<long>(type: "INTEGER", nullable: false),
                    is_revoked = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "teams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    short_name = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    country = table.Column<string>(type: "TEXT", nullable: false),
                    logo_url = table.Column<string>(type: "TEXT", nullable: true),
                    primary_color = table.Column<string>(type: "TEXT", nullable: true),
                    secondary_color = table.Column<string>(type: "TEXT", nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teams", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "venues",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    city = table.Column<string>(type: "TEXT", nullable: false),
                    state = table.Column<string>(type: "TEXT", nullable: false),
                    country = table.Column<string>(type: "TEXT", nullable: false),
                    capacity = table.Column<int>(type: "INTEGER", nullable: false),
                    timezone = table.Column<string>(type: "TEXT", nullable: false),
                    logo_url = table.Column<string>(type: "TEXT", nullable: true),
                    ground_image_url = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_venues", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    role_id = table.Column<string>(type: "TEXT", nullable: false),
                    claim_type = table.Column<string>(type: "TEXT", nullable: true),
                    claim_value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_role_claims_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "AspNetRoles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    user_id = table.Column<string>(type: "TEXT", nullable: false),
                    claim_type = table.Column<string>(type: "TEXT", nullable: true),
                    claim_value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_user_claims_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "TEXT", nullable: false),
                    provider_key = table.Column<string>(type: "TEXT", nullable: false),
                    provider_display_name = table.Column<string>(type: "TEXT", nullable: true),
                    user_id = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_asp_net_user_logins_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "TEXT", nullable: false),
                    role_id = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_asp_net_user_roles_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "AspNetRoles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asp_net_user_roles_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "TEXT", nullable: false),
                    login_provider = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_asp_net_user_tokens_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tournaments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    short_name = table.Column<string>(type: "TEXT", nullable: false),
                    season = table.Column<string>(type: "TEXT", nullable: false),
                    format = table.Column<int>(type: "INTEGER", nullable: false),
                    start_date = table.Column<long>(type: "INTEGER", nullable: false),
                    end_date = table.Column<long>(type: "INTEGER", nullable: false),
                    logo_url = table.Column<string>(type: "TEXT", nullable: true),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    default_match_rules_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    overlay_theme_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tournaments", x => x.id);
                    table.ForeignKey(
                        name: "fk_tournaments_match_rules_default_match_rules_id",
                        column: x => x.default_match_rules_id,
                        principalTable: "match_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_tournaments_overlay_themes_overlay_theme_id",
                        column: x => x.overlay_theme_id,
                        principalTable: "overlay_themes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    current_team_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    first_name = table.Column<string>(type: "TEXT", nullable: false),
                    last_name = table.Column<string>(type: "TEXT", nullable: false),
                    display_name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    short_name = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    photo_url = table.Column<string>(type: "TEXT", nullable: true),
                    jersey_number = table.Column<int>(type: "INTEGER", nullable: false),
                    batting_style = table.Column<string>(type: "TEXT", nullable: false),
                    bowling_style = table.Column<string>(type: "TEXT", nullable: false),
                    player_role = table.Column<int>(type: "INTEGER", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    career_label = table.Column<string>(type: "TEXT", nullable: false),
                    career_matches = table.Column<int>(type: "INTEGER", nullable: true),
                    career_runs = table.Column<int>(type: "INTEGER", nullable: true),
                    career_average = table.Column<decimal>(type: "TEXT", nullable: true),
                    career_strike_rate = table.Column<decimal>(type: "TEXT", nullable: true),
                    career_fifties = table.Column<int>(type: "INTEGER", nullable: true),
                    career_hundreds = table.Column<int>(type: "INTEGER", nullable: true),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_players", x => x.id);
                    table.ForeignKey(
                        name: "fk_players_teams_current_team_id",
                        column: x => x.current_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "matches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tournament_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    home_team_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    away_team_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    venue_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    match_rules_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    overlay_theme_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    match_format = table.Column<int>(type: "INTEGER", nullable: false),
                    status = table.Column<int>(type: "INTEGER", nullable: false),
                    scheduled_start = table.Column<long>(type: "INTEGER", nullable: true),
                    actual_start = table.Column<long>(type: "INTEGER", nullable: true),
                    actual_end = table.Column<long>(type: "INTEGER", nullable: true),
                    toss_winner_team_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    toss_decision = table.Column<int>(type: "INTEGER", nullable: true),
                    winner_team_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    result_type = table.Column<int>(type: "INTEGER", nullable: false),
                    result_text = table.Column<string>(type: "TEXT", nullable: true),
                    current_innings_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    break_kind = table.Column<int>(type: "INTEGER", nullable: true),
                    break_started_at = table.Column<long>(type: "INTEGER", nullable: true),
                    break_planned_seconds = table.Column<int>(type: "INTEGER", nullable: false),
                    pending_penalty_home = table.Column<int>(type: "INTEGER", nullable: false),
                    pending_penalty_away = table.Column<int>(type: "INTEGER", nullable: false),
                    current_day = table.Column<int>(type: "INTEGER", nullable: false),
                    current_session = table.Column<int>(type: "INTEGER", nullable: false),
                    overs_lost_today = table.Column<int>(type: "INTEGER", nullable: false),
                    win_prob_home = table.Column<int>(type: "INTEGER", nullable: false),
                    win_prob_draw = table.Column<int>(type: "INTEGER", nullable: false),
                    win_prob_away = table.Column<int>(type: "INTEGER", nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_matches", x => x.id);
                    table.ForeignKey(
                        name: "fk_matches_match_rules_match_rules_id",
                        column: x => x.match_rules_id,
                        principalTable: "match_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_matches_overlay_themes_overlay_theme_id",
                        column: x => x.overlay_theme_id,
                        principalTable: "overlay_themes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_matches_teams_away_team_id",
                        column: x => x.away_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_matches_teams_home_team_id",
                        column: x => x.home_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_matches_teams_toss_winner_team_id",
                        column: x => x.toss_winner_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_matches_teams_winner_team_id",
                        column: x => x.winner_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_matches_tournaments_tournament_id",
                        column: x => x.tournament_id,
                        principalTable: "tournaments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_matches_venues_venue_id",
                        column: x => x.venue_id,
                        principalTable: "venues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tournament_teams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    tournament_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    team_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tournament_teams", x => x.id);
                    table.ForeignKey(
                        name: "fk_tournament_teams_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tournament_teams_tournaments_tournament_id",
                        column: x => x.tournament_id,
                        principalTable: "tournaments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "innings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    match_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    batting_team_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    bowling_team_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    innings_number = table.Column<int>(type: "INTEGER", nullable: false),
                    status = table.Column<int>(type: "INTEGER", nullable: false),
                    is_follow_on = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_declared = table.Column<bool>(type: "INTEGER", nullable: false),
                    target = table.Column<int>(type: "INTEGER", nullable: true),
                    total_runs = table.Column<int>(type: "INTEGER", nullable: false),
                    total_wickets = table.Column<int>(type: "INTEGER", nullable: false),
                    total_extras = table.Column<int>(type: "INTEGER", nullable: false),
                    legal_balls = table.Column<int>(type: "INTEGER", nullable: false),
                    penalty_runs = table.Column<int>(type: "INTEGER", nullable: false),
                    current_striker_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    current_non_striker_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    current_bowler_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    last_bowler_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    free_hit_pending = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_super_over = table.Column<bool>(type: "INTEGER", nullable: false),
                    overs_limit = table.Column<int>(type: "INTEGER", nullable: true),
                    wicket_limit = table.Column<int>(type: "INTEGER", nullable: true),
                    is_dls = table.Column<bool>(type: "INTEGER", nullable: false),
                    dead_balls = table.Column<int>(type: "INTEGER", nullable: false),
                    power_surge_start_over = table.Column<int>(type: "INTEGER", nullable: true),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_innings", x => x.id);
                    table.ForeignKey(
                        name: "fk_innings_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_innings_teams_batting_team_id",
                        column: x => x.batting_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_innings_teams_bowling_team_id",
                        column: x => x.bowling_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "match_players",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    match_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    team_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    player_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    is_playing_xi = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_substitute = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_captain = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_wicket_keeper = table.Column<bool>(type: "INTEGER", nullable: false),
                    batting_order = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_players", x => x.id);
                    table.ForeignKey(
                        name: "fk_match_players_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_match_players_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_match_players_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "match_staff",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    match_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    team_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    role = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_staff", x => x.id);
                    table.ForeignKey(
                        name: "fk_match_staff_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_match_staff_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "overlay_graphic_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    match_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    graphic_key = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    is_visible = table.Column<bool>(type: "INTEGER", nullable: false),
                    payload_json = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_overlay_graphic_states", x => x.id);
                    table.ForeignKey(
                        name: "fk_overlay_graphic_states_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "innings_batter_statuses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    innings_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    player_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    kind = table.Column<int>(type: "INTEGER", nullable: false),
                    after_ball_sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    reason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_innings_batter_statuses", x => x.id);
                    table.ForeignKey(
                        name: "fk_innings_batter_statuses_innings_innings_id",
                        column: x => x.innings_id,
                        principalTable: "innings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_innings_batter_statuses_players_player_id",
                        column: x => x.player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "overs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    innings_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    bowler_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    over_number = table.Column<int>(type: "INTEGER", nullable: false),
                    is_completed = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_overs", x => x.id);
                    table.ForeignKey(
                        name: "fk_overs_innings_innings_id",
                        column: x => x.innings_id,
                        principalTable: "innings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_overs_players_bowler_id",
                        column: x => x.bowler_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "balls",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    over_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    innings_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    striker_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    non_striker_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    bowler_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    runs_bat = table.Column<int>(type: "INTEGER", nullable: false),
                    runs_extra = table.Column<int>(type: "INTEGER", nullable: false),
                    bowler_runs = table.Column<int>(type: "INTEGER", nullable: false),
                    short_runs = table.Column<int>(type: "INTEGER", nullable: false),
                    runs_completed = table.Column<int>(type: "INTEGER", nullable: false),
                    extras_type = table.Column<int>(type: "INTEGER", nullable: false),
                    secondary_extras_type = table.Column<int>(type: "INTEGER", nullable: false),
                    is_boundary = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_four = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_six = table.Column<bool>(type: "INTEGER", nullable: false),
                    six_distance = table.Column<int>(type: "INTEGER", nullable: true),
                    is_legal = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_free_hit = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_wicket = table.Column<bool>(type: "INTEGER", nullable: false),
                    day = table.Column<int>(type: "INTEGER", nullable: false),
                    session = table.Column<int>(type: "INTEGER", nullable: false),
                    is_voided = table.Column<bool>(type: "INTEGER", nullable: false),
                    voided_reason = table.Column<string>(type: "TEXT", nullable: true),
                    voided_by = table.Column<string>(type: "TEXT", nullable: true),
                    scored_by = table.Column<string>(type: "TEXT", nullable: false),
                    client_request_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_balls", x => x.id);
                    table.ForeignKey(
                        name: "fk_balls_innings_innings_id",
                        column: x => x.innings_id,
                        principalTable: "innings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_balls_overs_over_id",
                        column: x => x.over_id,
                        principalTable: "overs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_balls_players_bowler_id",
                        column: x => x.bowler_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_balls_players_non_striker_id",
                        column: x => x.non_striker_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_balls_players_striker_id",
                        column: x => x.striker_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dismissals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ball_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    dismissed_player_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    fielder_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    new_batter_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    dismissal_type = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dismissals", x => x.id);
                    table.ForeignKey(
                        name: "fk_dismissals_balls_ball_id",
                        column: x => x.ball_id,
                        principalTable: "balls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_dismissals_players_dismissed_player_id",
                        column: x => x.dismissed_player_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_dismissals_players_fielder_id",
                        column: x => x.fielder_id,
                        principalTable: "players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_role_claims_role_id",
                table: "AspNetRoleClaims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_claims_user_id",
                table: "AspNetUserClaims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_logins_user_id",
                table: "AspNetUserLogins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_roles_role_id",
                table: "AspNetUserRoles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_match_id_created_at",
                table: "audit_logs",
                columns: new[] { "match_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_balls_bowler_id",
                table: "balls",
                column: "bowler_id");

            migrationBuilder.CreateIndex(
                name: "ix_balls_client_request_id",
                table: "balls",
                column: "client_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_balls_innings_id_sequence",
                table: "balls",
                columns: new[] { "innings_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_balls_non_striker_id",
                table: "balls",
                column: "non_striker_id");

            migrationBuilder.CreateIndex(
                name: "ix_balls_over_id",
                table: "balls",
                column: "over_id");

            migrationBuilder.CreateIndex(
                name: "ix_balls_striker_id",
                table: "balls",
                column: "striker_id");

            migrationBuilder.CreateIndex(
                name: "ix_dismissals_ball_id",
                table: "dismissals",
                column: "ball_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dismissals_dismissed_player_id",
                table: "dismissals",
                column: "dismissed_player_id");

            migrationBuilder.CreateIndex(
                name: "ix_dismissals_fielder_id",
                table: "dismissals",
                column: "fielder_id");

            migrationBuilder.CreateIndex(
                name: "ix_innings_batting_team_id",
                table: "innings",
                column: "batting_team_id");

            migrationBuilder.CreateIndex(
                name: "ix_innings_bowling_team_id",
                table: "innings",
                column: "bowling_team_id");

            migrationBuilder.CreateIndex(
                name: "ix_innings_match_id_innings_number",
                table: "innings",
                columns: new[] { "match_id", "innings_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_innings_batter_statuses_innings_id_player_id",
                table: "innings_batter_statuses",
                columns: new[] { "innings_id", "player_id" });

            migrationBuilder.CreateIndex(
                name: "ix_innings_batter_statuses_player_id",
                table: "innings_batter_statuses",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_match_players_match_id_player_id",
                table: "match_players",
                columns: new[] { "match_id", "player_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_match_players_player_id",
                table: "match_players",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_match_players_team_id",
                table: "match_players",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "ix_match_rules_name",
                table: "match_rules",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_match_staff_match_id_team_id",
                table: "match_staff",
                columns: new[] { "match_id", "team_id" });

            migrationBuilder.CreateIndex(
                name: "ix_match_staff_team_id",
                table: "match_staff",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_away_team_id",
                table: "matches",
                column: "away_team_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_home_team_id",
                table: "matches",
                column: "home_team_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_match_rules_id",
                table: "matches",
                column: "match_rules_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_overlay_theme_id",
                table: "matches",
                column: "overlay_theme_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_status_scheduled_start",
                table: "matches",
                columns: new[] { "status", "scheduled_start" });

            migrationBuilder.CreateIndex(
                name: "ix_matches_toss_winner_team_id",
                table: "matches",
                column: "toss_winner_team_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_tournament_id",
                table: "matches",
                column: "tournament_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_venue_id",
                table: "matches",
                column: "venue_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_winner_team_id",
                table: "matches",
                column: "winner_team_id");

            migrationBuilder.CreateIndex(
                name: "ix_overlay_graphic_states_match_id_graphic_key",
                table: "overlay_graphic_states",
                columns: new[] { "match_id", "graphic_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_overs_bowler_id",
                table: "overs",
                column: "bowler_id");

            migrationBuilder.CreateIndex(
                name: "ix_overs_innings_id_over_number",
                table: "overs",
                columns: new[] { "innings_id", "over_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_players_current_team_id",
                table: "players",
                column: "current_team_id");

            migrationBuilder.CreateIndex(
                name: "ix_players_display_name",
                table: "players",
                column: "display_name");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token",
                table: "refresh_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_teams_name",
                table: "teams",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tournament_teams_team_id",
                table: "tournament_teams",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "ix_tournament_teams_tournament_id_team_id",
                table: "tournament_teams",
                columns: new[] { "tournament_id", "team_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tournaments_default_match_rules_id",
                table: "tournaments",
                column: "default_match_rules_id");

            migrationBuilder.CreateIndex(
                name: "ix_tournaments_overlay_theme_id",
                table: "tournaments",
                column: "overlay_theme_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "dismissals");

            migrationBuilder.DropTable(
                name: "innings_batter_statuses");

            migrationBuilder.DropTable(
                name: "match_players");

            migrationBuilder.DropTable(
                name: "match_staff");

            migrationBuilder.DropTable(
                name: "overlay_graphic_states");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "tournament_teams");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "balls");

            migrationBuilder.DropTable(
                name: "overs");

            migrationBuilder.DropTable(
                name: "innings");

            migrationBuilder.DropTable(
                name: "players");

            migrationBuilder.DropTable(
                name: "matches");

            migrationBuilder.DropTable(
                name: "teams");

            migrationBuilder.DropTable(
                name: "tournaments");

            migrationBuilder.DropTable(
                name: "venues");

            migrationBuilder.DropTable(
                name: "match_rules");

            migrationBuilder.DropTable(
                name: "overlay_themes");
        }
    }
}
