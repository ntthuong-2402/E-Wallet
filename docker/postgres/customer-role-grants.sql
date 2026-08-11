\set ON_ERROR_STOP on

-- Required psql variables:
--   customer_runtime_role
--   customer_retention_role
-- Run as the Customer schema/migration owner after Customer migrations.

REVOKE ALL ON SCHEMA customer FROM PUBLIC;
GRANT USAGE ON SCHEMA customer TO :"customer_runtime_role", :"customer_retention_role";

GRANT SELECT, INSERT, UPDATE ON TABLE customer.customers TO :"customer_runtime_role";
GRANT SELECT, INSERT ON TABLE customer.customer_change_audits TO :"customer_runtime_role";
REVOKE UPDATE, DELETE ON TABLE customer.customer_change_audits FROM :"customer_runtime_role";

GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA customer TO :"customer_runtime_role";
GRANT SELECT ON TABLE customer."__EFMigrationsHistory" TO :"customer_runtime_role";

REVOKE ALL ON FUNCTION customer.purge_expired_customer_audits(integer) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION customer.purge_expired_customer_audits(integer)
    TO :"customer_retention_role";
