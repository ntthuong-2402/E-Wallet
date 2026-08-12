using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Friday.Modules.PaymentLedger.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PaymentLedgerAuditAndIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "financial_audit_records",
                schema: "payment_ledger",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActorUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RefId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LedgerAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    FinancialTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_audit_records", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_financial_audit_records_ActorUserId_OccurredOnUtc",
                schema: "payment_ledger",
                table: "financial_audit_records",
                columns: new[] { "ActorUserId", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_audit_records_FinancialTransactionId_OccurredOnUtc",
                schema: "payment_ledger",
                table: "financial_audit_records",
                columns: new[] { "FinancialTransactionId", "OccurredOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_audit_records_LedgerAccountId_OccurredOnUtc",
                schema: "payment_ledger",
                table: "financial_audit_records",
                columns: new[] { "LedgerAccountId", "OccurredOnUtc" });

            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_financial_audit_records_append_only
                BEFORE UPDATE OR DELETE ON payment_ledger.financial_audit_records
                FOR EACH ROW EXECUTE FUNCTION payment_ledger.reject_posted_ledger_mutation();
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_financial_audit_records_append_only
                    ON payment_ledger.financial_audit_records;
                """
            );

            migrationBuilder.DropTable(
                name: "financial_audit_records",
                schema: "payment_ledger");
        }
    }
}
