using Friday.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Friday.BuildingBlocks.Infrastructure.Migrations;

[DbContext(typeof(FridayDbContext))]
[Migration("20260810153000_AdminP1Integrity")]
public sealed class AdminP1Integrity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_user_sessions_RefreshTokenHash",
            schema: "admin",
            table: "user_sessions"
        );

        migrationBuilder.CreateIndex(
            name: "IX_user_sessions_RefreshTokenHash",
            schema: "admin",
            table: "user_sessions",
            column: "RefreshTokenHash",
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "IX_user_roles_RoleId",
            schema: "admin",
            table: "user_roles",
            column: "RoleId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_role_rights_RightId",
            schema: "admin",
            table: "role_rights",
            column: "RightId"
        );

        migrationBuilder.AddForeignKey(
            name: "FK_user_roles_roles_RoleId",
            schema: "admin",
            table: "user_roles",
            column: "RoleId",
            principalSchema: "admin",
            principalTable: "roles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict
        );

        migrationBuilder.AddForeignKey(
            name: "FK_role_rights_rights_RightId",
            schema: "admin",
            table: "role_rights",
            column: "RightId",
            principalSchema: "admin",
            principalTable: "rights",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_user_roles_roles_RoleId",
            schema: "admin",
            table: "user_roles"
        );

        migrationBuilder.DropForeignKey(
            name: "FK_role_rights_rights_RightId",
            schema: "admin",
            table: "role_rights"
        );

        migrationBuilder.DropIndex(
            name: "IX_user_roles_RoleId",
            schema: "admin",
            table: "user_roles"
        );

        migrationBuilder.DropIndex(
            name: "IX_role_rights_RightId",
            schema: "admin",
            table: "role_rights"
        );

        migrationBuilder.DropIndex(
            name: "IX_user_sessions_RefreshTokenHash",
            schema: "admin",
            table: "user_sessions"
        );

        migrationBuilder.CreateIndex(
            name: "IX_user_sessions_RefreshTokenHash",
            schema: "admin",
            table: "user_sessions",
            column: "RefreshTokenHash"
        );
    }
}
