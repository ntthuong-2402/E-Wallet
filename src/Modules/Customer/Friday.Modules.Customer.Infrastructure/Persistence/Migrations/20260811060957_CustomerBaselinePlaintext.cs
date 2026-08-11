using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Friday.Modules.Customer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerBaselinePlaintext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "customer");

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "customer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    CitizenDocumentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CitizenIssuingCountryCode = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    CitizenDocumentNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CitizenIdMasked = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OpenedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.Id);
                    table.CheckConstraint("CK_customers_country", "\"CitizenIssuingCountryCode\" ~ '^[A-Z]{2}$'");
                    table.CheckConstraint("CK_customers_document_format", "(\"CitizenDocumentType\" = 'VietnamCitizenId' AND \"CitizenIssuingCountryCode\" = 'VN' AND \"CitizenDocumentNumber\" ~ '^[0-9]{12}$') OR (\"CitizenDocumentType\" = 'Passport' AND \"CitizenDocumentNumber\" ~ '^[A-Z0-9]{6,20}$')");
                    table.CheckConstraint("CK_customers_document_mask", "\"CitizenIdMasked\" = repeat('*', greatest(char_length(\"CitizenDocumentNumber\") - 4, 0)) || right(\"CitizenDocumentNumber\", least(4, char_length(\"CitizenDocumentNumber\")))");
                    table.CheckConstraint("CK_customers_document_type", "\"CitizenDocumentType\" IN ('VietnamCitizenId', 'Passport')");
                    table.CheckConstraint("CK_customers_status", "\"Status\" IN ('Active', 'Suspended', 'Closed')");
                });

            migrationBuilder.CreateTable(
                name: "customer_change_audits",
                schema: "customer",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    CustomerCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EventType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ChangedFieldsJson = table.Column<string>(type: "jsonb", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RetainUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_change_audits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_change_audits_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "customer",
                        principalTable: "customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_change_audits_CustomerId_OccurredOnUtc",
                schema: "customer",
                table: "customer_change_audits",
                columns: new[] { "CustomerId", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_change_audits_RetainUntilUtc",
                schema: "customer",
                table: "customer_change_audits",
                column: "RetainUntilUtc");

            migrationBuilder.CreateIndex(
                name: "IX_customers_CitizenDocumentType_CitizenIssuingCountryCode_Cit~",
                schema: "customer",
                table: "customers",
                columns: new[] { "CitizenDocumentType", "CitizenIssuingCountryCode", "CitizenDocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_CustomerCode",
                schema: "customer",
                table: "customers",
                column: "CustomerCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_OpenedOnUtc_Id",
                schema: "customer",
                table: "customers",
                columns: new[] { "OpenedOnUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_customers_Status_OpenedOnUtc_Id",
                schema: "customer",
                table: "customers",
                columns: new[] { "Status", "OpenedOnUtc", "Id" });

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION customer.enforce_customer_audit_append_only()
                RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    IF TG_OP = 'DELETE' AND current_user <> session_user THEN
                        RETURN OLD;
                    END IF;
                    RAISE EXCEPTION 'Customer audit records are append-only.' USING ERRCODE = '55000';
                END;
                $function$;

                CREATE TRIGGER customer_audit_append_only
                BEFORE UPDATE OR DELETE ON customer.customer_change_audits
                FOR EACH ROW EXECUTE FUNCTION customer.enforce_customer_audit_append_only();

                CREATE OR REPLACE FUNCTION customer.purge_expired_customer_audits(batch_size integer)
                RETURNS integer
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog, customer
                AS $function$
                DECLARE deleted_count integer;
                BEGIN
                    IF batch_size < 1 OR batch_size > 10000 THEN
                        RAISE EXCEPTION 'Retention batch size must be between 1 and 10000.';
                    END IF;
                    WITH candidates AS (
                        SELECT "Id" FROM customer.customer_change_audits
                        WHERE "RetainUntilUtc" <= clock_timestamp()
                        ORDER BY "RetainUntilUtc", "Id"
                        FOR UPDATE SKIP LOCKED
                        LIMIT batch_size
                    )
                    DELETE FROM customer.customer_change_audits audit
                    USING candidates
                    WHERE audit."Id" = candidates."Id";
                    GET DIAGNOSTICS deleted_count = ROW_COUNT;
                    RETURN deleted_count;
                END;
                $function$;

                REVOKE ALL ON FUNCTION customer.purge_expired_customer_audits(integer) FROM PUBLIC;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS customer.purge_expired_customer_audits(integer);
                DROP TRIGGER IF EXISTS customer_audit_append_only ON customer.customer_change_audits;
                DROP FUNCTION IF EXISTS customer.enforce_customer_audit_append_only();
                """);

            migrationBuilder.DropTable(
                name: "customer_change_audits",
                schema: "customer");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "customer");
        }
    }
}
