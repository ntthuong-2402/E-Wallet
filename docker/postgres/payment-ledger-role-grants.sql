\set ON_ERROR_STOP on

-- Required psql variable: payment_ledger_runtime_role
-- Run as the PaymentLedger schema/migration owner after PaymentLedger migrations.

REVOKE ALL ON SCHEMA payment_ledger FROM PUBLIC;
GRANT USAGE ON SCHEMA payment_ledger TO :"payment_ledger_runtime_role";

GRANT SELECT, INSERT, UPDATE ON TABLE payment_ledger.ledger_accounts
    TO :"payment_ledger_runtime_role";
GRANT SELECT, INSERT, UPDATE ON TABLE payment_ledger.financial_transactions
    TO :"payment_ledger_runtime_role";
REVOKE DELETE ON TABLE payment_ledger.financial_transactions
    FROM :"payment_ledger_runtime_role";

GRANT SELECT, INSERT ON TABLE payment_ledger.journals,
    payment_ledger.journal_entries,
    payment_ledger.idempotency_records,
    payment_ledger.financial_audit_records
    TO :"payment_ledger_runtime_role";
REVOKE UPDATE, DELETE ON TABLE payment_ledger.journals,
    payment_ledger.journal_entries,
    payment_ledger.idempotency_records,
    payment_ledger.financial_audit_records
    FROM :"payment_ledger_runtime_role";

GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA payment_ledger
    TO :"payment_ledger_runtime_role";
GRANT SELECT ON TABLE payment_ledger."__EFMigrationsHistory"
    TO :"payment_ledger_runtime_role";
