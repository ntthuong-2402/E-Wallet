using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Friday.Modules.Customer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerRefIdAndAccountLinkage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_account_linkages",
                schema: "customer",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LinkedByActorUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LinkedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LinkReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UnlinkedByActorUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    UnlinkedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UnlinkReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_account_linkages", x => x.Id);
                    table.CheckConstraint("CK_customer_account_linkages_account_id", "char_length(\"AccountId\") BETWEEN 1 AND 128 AND btrim(\"AccountId\") = \"AccountId\"");
                    table.CheckConstraint("CK_customer_account_linkages_link_reason", "char_length(btrim(\"LinkReason\")) BETWEEN 1 AND 500");
                    table.CheckConstraint("CK_customer_account_linkages_unlink_state", "(\"UnlinkedOnUtc\" IS NULL AND \"UnlinkedByActorUserId\" IS NULL AND \"UnlinkReason\" IS NULL) OR (\"UnlinkedOnUtc\" IS NOT NULL AND \"UnlinkedOnUtc\" >= \"LinkedOnUtc\" AND char_length(btrim(\"UnlinkedByActorUserId\")) BETWEEN 1 AND 128 AND char_length(btrim(\"UnlinkReason\")) BETWEEN 1 AND 500)");
                    table.ForeignKey(
                        name: "FK_customer_account_linkages_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "customer",
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_create_references",
                schema: "customer",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RefId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ActorUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    ResponseJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_create_references", x => x.Id);
                    table.CheckConstraint("CK_customer_create_references_ref_id", "\"RefId\" ~ '^[A-Za-z0-9._:-]{1,100}$'");
                    table.CheckConstraint("CK_customer_create_references_request_hash", "\"RequestHash\" ~ '^[A-F0-9]{64}$'");
                    table.ForeignKey(
                        name: "FK_customer_create_references_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "customer",
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_account_linkages_AccountId",
                schema: "customer",
                table: "customer_account_linkages",
                column: "AccountId",
                unique: true,
                filter: "\"UnlinkedOnUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_customer_account_linkages_CustomerId",
                schema: "customer",
                table: "customer_account_linkages",
                column: "CustomerId",
                unique: true,
                filter: "\"UnlinkedOnUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_customer_create_references_CustomerId",
                schema: "customer",
                table: "customer_create_references",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_create_references_RefId",
                schema: "customer",
                table: "customer_create_references",
                column: "RefId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_account_linkages",
                schema: "customer");

            migrationBuilder.DropTable(
                name: "customer_create_references",
                schema: "customer");
        }
    }
}
