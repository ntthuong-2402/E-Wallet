using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Friday.BuildingBlocks.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminSecurityBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedLoginCount",
                schema: "admin",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastFailedLoginAtUtc",
                schema: "admin",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEndUtc",
                schema: "admin",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                schema: "admin",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReplacedAtUtc",
                schema: "admin",
                table: "user_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReuseDetectedAtUtc",
                schema: "admin",
                table: "user_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TokenFamilyId",
                schema: "admin",
                table: "user_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE admin.user_sessions SET \"TokenFamilyId\" = \"Id\" WHERE \"TokenFamilyId\" IS NULL;"
            );

            migrationBuilder.AlterColumn<Guid>(
                name: "TokenFamilyId",
                schema: "admin",
                table: "user_sessions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "admin",
                table: "user_sessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "security_audit_events",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
                    TargetType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TargetId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    OccurredOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_audit_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_TokenFamilyId",
                schema: "admin",
                table: "user_sessions",
                column: "TokenFamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_security_audit_events_ActorUserId_OccurredOnUtc",
                schema: "admin",
                table: "security_audit_events",
                columns: new[] { "ActorUserId", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_security_audit_events_EventType_OccurredOnUtc",
                schema: "admin",
                table: "security_audit_events",
                columns: new[] { "EventType", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_security_audit_events_OccurredOnUtc",
                schema: "admin",
                table: "security_audit_events",
                column: "OccurredOnUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "security_audit_events",
                schema: "admin");

            migrationBuilder.DropIndex(
                name: "IX_user_sessions_TokenFamilyId",
                schema: "admin",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "FailedLoginCount",
                schema: "admin",
                table: "users");

            migrationBuilder.DropColumn(
                name: "LastFailedLoginAtUtc",
                schema: "admin",
                table: "users");

            migrationBuilder.DropColumn(
                name: "LockoutEndUtc",
                schema: "admin",
                table: "users");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                schema: "admin",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ReplacedAtUtc",
                schema: "admin",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "ReuseDetectedAtUtc",
                schema: "admin",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "TokenFamilyId",
                schema: "admin",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "admin",
                table: "user_sessions");
        }
    }
}
