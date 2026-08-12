using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Friday.Modules.PaymentLedger.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PaymentLedgerBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "payment_ledger");

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                schema: "payment_ledger",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActorUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Operation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RefId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResponseJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ledger_accounts",
                schema: "payment_ledger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountRef = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AvailableBalance = table.Column<decimal>(type: "numeric(19,0)", precision: 19, scale: 0, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_accounts", x => x.Id);
                    table.CheckConstraint("CK_ledger_accounts_balance", "\"AvailableBalance\" >= 0 AND \"AvailableBalance\" = trunc(\"AvailableBalance\")");
                    table.CheckConstraint("CK_ledger_accounts_currency", "\"Currency\" = 'VND'");
                    table.CheckConstraint("CK_ledger_accounts_status", "\"Status\" IN ('Active','Suspended')");
                });

            migrationBuilder.CreateTable(
                name: "financial_transactions",
                schema: "payment_ledger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SourceAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,0)", precision: 19, scale: 0, nullable: false),
                    Currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OriginalTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversalTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_transactions", x => x.Id);
                    table.CheckConstraint("CK_financial_transactions_accounts", "\"SourceAccountId\" <> \"DestinationAccountId\"");
                    table.CheckConstraint("CK_financial_transactions_amount", "\"Amount\" > 0 AND \"Amount\" = trunc(\"Amount\")");
                    table.CheckConstraint("CK_financial_transactions_currency", "\"Currency\" = 'VND'");
                    table.CheckConstraint("CK_financial_transactions_status", "\"Status\" IN ('Posted','Reversed')");
                    table.CheckConstraint("CK_financial_transactions_type", "\"Type\" IN ('InternalTransfer','Reversal')");
                    table.ForeignKey(
                        name: "FK_financial_transactions_financial_transactions_OriginalTrans~",
                        column: x => x.OriginalTransactionId,
                        principalSchema: "payment_ledger",
                        principalTable: "financial_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financial_transactions_financial_transactions_ReversalTrans~",
                        column: x => x.ReversalTransactionId,
                        principalSchema: "payment_ledger",
                        principalTable: "financial_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financial_transactions_ledger_accounts_DestinationAccountId",
                        column: x => x.DestinationAccountId,
                        principalSchema: "payment_ledger",
                        principalTable: "ledger_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financial_transactions_ledger_accounts_SourceAccountId",
                        column: x => x.SourceAccountId,
                        principalSchema: "payment_ledger",
                        principalTable: "ledger_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "journals",
                schema: "payment_ledger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_journals_financial_transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalSchema: "payment_ledger",
                        principalTable: "financial_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "journal_entries",
                schema: "payment_ledger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalId = table.Column<Guid>(type: "uuid", nullable: false),
                    LedgerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,0)", precision: 19, scale: 0, nullable: false),
                    Currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entries", x => x.Id);
                    table.CheckConstraint("CK_journal_entries_amount", "\"Amount\" > 0 AND \"Amount\" = trunc(\"Amount\")");
                    table.CheckConstraint("CK_journal_entries_currency", "\"Currency\" = 'VND'");
                    table.CheckConstraint("CK_journal_entries_direction", "\"Direction\" IN ('Debit','Credit')");
                    table.ForeignKey(
                        name: "FK_journal_entries_journals_JournalId",
                        column: x => x.JournalId,
                        principalSchema: "payment_ledger",
                        principalTable: "journals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_journal_entries_ledger_accounts_LedgerAccountId",
                        column: x => x.LedgerAccountId,
                        principalSchema: "payment_ledger",
                        principalTable: "ledger_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_DestinationAccountId_PostedOnUtc",
                schema: "payment_ledger",
                table: "financial_transactions",
                columns: new[] { "DestinationAccountId", "PostedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_OriginalTransactionId",
                schema: "payment_ledger",
                table: "financial_transactions",
                column: "OriginalTransactionId",
                unique: true,
                filter: "\"OriginalTransactionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_PostedOnUtc_Id",
                schema: "payment_ledger",
                table: "financial_transactions",
                columns: new[] { "PostedOnUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_ReversalTransactionId",
                schema: "payment_ledger",
                table: "financial_transactions",
                column: "ReversalTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_transactions_SourceAccountId_PostedOnUtc",
                schema: "payment_ledger",
                table: "financial_transactions",
                columns: new[] { "SourceAccountId", "PostedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_records_ActorUserId_Operation_RefId",
                schema: "payment_ledger",
                table: "idempotency_records",
                columns: new[] { "ActorUserId", "Operation", "RefId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_JournalId",
                schema: "payment_ledger",
                table: "journal_entries",
                column: "JournalId");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_LedgerAccountId_JournalId",
                schema: "payment_ledger",
                table: "journal_entries",
                columns: new[] { "LedgerAccountId", "JournalId" });

            migrationBuilder.CreateIndex(
                name: "IX_journals_TransactionId",
                schema: "payment_ledger",
                table: "journals",
                column: "TransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ledger_accounts_AccountRef",
                schema: "payment_ledger",
                table: "ledger_accounts",
                column: "AccountRef",
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE FUNCTION payment_ledger.reject_posted_ledger_mutation()
                RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    RAISE EXCEPTION 'Posted ledger records are append-only.'
                        USING ERRCODE = '55000';
                END;
                $function$;

                CREATE TRIGGER trg_journals_append_only
                BEFORE UPDATE OR DELETE ON payment_ledger.journals
                FOR EACH ROW EXECUTE FUNCTION payment_ledger.reject_posted_ledger_mutation();

                CREATE TRIGGER trg_journal_entries_append_only
                BEFORE UPDATE OR DELETE ON payment_ledger.journal_entries
                FOR EACH ROW EXECUTE FUNCTION payment_ledger.reject_posted_ledger_mutation();

                CREATE FUNCTION payment_ledger.validate_posted_journal()
                RETURNS trigger LANGUAGE plpgsql AS $function$
                DECLARE
                    target_journal_id uuid;
                    entry_count integer;
                    matching_debit_count integer;
                    matching_credit_count integer;
                BEGIN
                    target_journal_id := COALESCE(
                        (to_jsonb(NEW) ->> 'JournalId')::uuid,
                        (to_jsonb(NEW) ->> 'Id')::uuid
                    );

                    SELECT
                        COUNT(*),
                        COUNT(*) FILTER (
                            WHERE entry."Direction" = 'Debit'
                              AND entry."LedgerAccountId" = transaction."SourceAccountId"
                              AND entry."Amount" = transaction."Amount"
                              AND entry."Currency" = transaction."Currency"
                        ),
                        COUNT(*) FILTER (
                            WHERE entry."Direction" = 'Credit'
                              AND entry."LedgerAccountId" = transaction."DestinationAccountId"
                              AND entry."Amount" = transaction."Amount"
                              AND entry."Currency" = transaction."Currency"
                        )
                    INTO entry_count, matching_debit_count, matching_credit_count
                    FROM payment_ledger.journals journal
                    JOIN payment_ledger.financial_transactions transaction
                      ON transaction."Id" = journal."TransactionId"
                    LEFT JOIN payment_ledger.journal_entries entry
                      ON entry."JournalId" = journal."Id"
                    WHERE journal."Id" = target_journal_id
                    GROUP BY transaction."Id";

                    IF entry_count <> 2
                       OR matching_debit_count <> 1
                       OR matching_credit_count <> 1 THEN
                        RAISE EXCEPTION 'Posted journal does not match its financial transaction.'
                            USING ERRCODE = '23514';
                    END IF;
                    RETURN NULL;
                END;
                $function$;

                CREATE CONSTRAINT TRIGGER trg_journals_valid_posting
                AFTER INSERT ON payment_ledger.journals
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION payment_ledger.validate_posted_journal();

                CREATE CONSTRAINT TRIGGER trg_journal_entries_valid_posting
                AFTER INSERT ON payment_ledger.journal_entries
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION payment_ledger.validate_posted_journal();

                CREATE FUNCTION payment_ledger.protect_financial_transaction()
                RETURNS trigger LANGUAGE plpgsql AS $function$
                BEGIN
                    IF TG_OP = 'UPDATE'
                       AND OLD."Type" = 'InternalTransfer'
                       AND OLD."Status" = 'Posted'
                       AND OLD."ReversalTransactionId" IS NULL
                       AND NEW."Status" = 'Reversed'
                       AND NEW."ReversalTransactionId" IS NOT NULL
                       AND NEW."Id" = OLD."Id"
                       AND NEW."Type" = OLD."Type"
                       AND NEW."SourceAccountId" = OLD."SourceAccountId"
                       AND NEW."DestinationAccountId" = OLD."DestinationAccountId"
                       AND NEW."Amount" = OLD."Amount"
                       AND NEW."Currency" = OLD."Currency"
                       AND NEW."Description" IS NOT DISTINCT FROM OLD."Description"
                       AND NEW."OriginalTransactionId" IS NOT DISTINCT FROM OLD."OriginalTransactionId"
                       AND NEW."PostedOnUtc" = OLD."PostedOnUtc" THEN
                        RETURN NEW;
                    END IF;

                    RAISE EXCEPTION 'Posted financial transaction economics are immutable.'
                        USING ERRCODE = '55000';
                END;
                $function$;

                CREATE TRIGGER trg_financial_transactions_immutable
                BEFORE UPDATE OR DELETE ON payment_ledger.financial_transactions
                FOR EACH ROW EXECUTE FUNCTION payment_ledger.protect_financial_transaction();

                CREATE FUNCTION payment_ledger.validate_reversal_link()
                RETURNS trigger LANGUAGE plpgsql AS $function$
                DECLARE
                    counterpart payment_ledger.financial_transactions%ROWTYPE;
                BEGIN
                    IF NEW."Type" = 'InternalTransfer' AND NEW."Status" = 'Posted' THEN
                        IF NEW."OriginalTransactionId" IS NOT NULL
                           OR NEW."ReversalTransactionId" IS NOT NULL THEN
                            RAISE EXCEPTION 'Posted transfer has invalid reversal links.'
                                USING ERRCODE = '23514';
                        END IF;
                        RETURN NULL;
                    END IF;

                    IF NEW."Type" = 'InternalTransfer' AND NEW."Status" = 'Reversed' THEN
                        IF NEW."OriginalTransactionId" IS NOT NULL
                           OR NEW."ReversalTransactionId" IS NULL THEN
                            RAISE EXCEPTION 'Reversed transfer requires one reversal.'
                                USING ERRCODE = '23514';
                        END IF;
                        SELECT * INTO counterpart
                        FROM payment_ledger.financial_transactions
                        WHERE "Id" = NEW."ReversalTransactionId";
                        IF NOT FOUND
                           OR counterpart."Type" <> 'Reversal'
                           OR counterpart."Status" <> 'Posted'
                           OR counterpart."OriginalTransactionId" <> NEW."Id"
                           OR counterpart."SourceAccountId" <> NEW."DestinationAccountId"
                           OR counterpart."DestinationAccountId" <> NEW."SourceAccountId"
                           OR counterpart."Amount" <> NEW."Amount"
                           OR counterpart."Currency" <> NEW."Currency" THEN
                            RAISE EXCEPTION 'Reversal is not reciprocal to the original transfer.'
                                USING ERRCODE = '23514';
                        END IF;
                        RETURN NULL;
                    END IF;

                    IF NEW."Type" = 'Reversal' AND NEW."Status" = 'Posted' THEN
                        IF NEW."OriginalTransactionId" IS NULL
                           OR NEW."ReversalTransactionId" IS NOT NULL THEN
                            RAISE EXCEPTION 'Reversal has invalid links.'
                                USING ERRCODE = '23514';
                        END IF;
                        SELECT * INTO counterpart
                        FROM payment_ledger.financial_transactions
                        WHERE "Id" = NEW."OriginalTransactionId";
                        IF NOT FOUND
                           OR counterpart."Type" <> 'InternalTransfer'
                           OR counterpart."Status" <> 'Reversed'
                           OR counterpart."ReversalTransactionId" <> NEW."Id"
                           OR NEW."SourceAccountId" <> counterpart."DestinationAccountId"
                           OR NEW."DestinationAccountId" <> counterpart."SourceAccountId"
                           OR NEW."Amount" <> counterpart."Amount"
                           OR NEW."Currency" <> counterpart."Currency" THEN
                            RAISE EXCEPTION 'Reversal is not reciprocal to the original transfer.'
                                USING ERRCODE = '23514';
                        END IF;
                        RETURN NULL;
                    END IF;

                    RAISE EXCEPTION 'Financial transaction state and type are inconsistent.'
                        USING ERRCODE = '23514';
                END;
                $function$;

                CREATE CONSTRAINT TRIGGER trg_financial_transactions_reversal_integrity
                AFTER INSERT OR UPDATE ON payment_ledger.financial_transactions
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION payment_ledger.validate_reversal_link();
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_financial_transactions_reversal_integrity
                    ON payment_ledger.financial_transactions;
                DROP FUNCTION IF EXISTS payment_ledger.validate_reversal_link();
                DROP TRIGGER IF EXISTS trg_financial_transactions_immutable
                    ON payment_ledger.financial_transactions;
                DROP FUNCTION IF EXISTS payment_ledger.protect_financial_transaction();
                DROP TRIGGER IF EXISTS trg_journal_entries_valid_posting
                    ON payment_ledger.journal_entries;
                DROP TRIGGER IF EXISTS trg_journals_valid_posting
                    ON payment_ledger.journals;
                DROP FUNCTION IF EXISTS payment_ledger.validate_posted_journal();
                DROP TRIGGER IF EXISTS trg_journal_entries_append_only
                    ON payment_ledger.journal_entries;
                DROP TRIGGER IF EXISTS trg_journals_append_only
                    ON payment_ledger.journals;
                DROP FUNCTION IF EXISTS payment_ledger.reject_posted_ledger_mutation();
                """
            );

            migrationBuilder.DropTable(
                name: "idempotency_records",
                schema: "payment_ledger");

            migrationBuilder.DropTable(
                name: "journal_entries",
                schema: "payment_ledger");

            migrationBuilder.DropTable(
                name: "journals",
                schema: "payment_ledger");

            migrationBuilder.DropTable(
                name: "financial_transactions",
                schema: "payment_ledger");

            migrationBuilder.DropTable(
                name: "ledger_accounts",
                schema: "payment_ledger");
        }
    }
}
