# Customer Operations Runbook

## Database roles

Provision separate PostgreSQL principals for migration/schema ownership, API
runtime, and retention. After migrations, apply the grant template:

```text
psql <migration-connection> \
  --set=customer_runtime_role=<runtime-role> \
  --set=customer_retention_role=<retention-role> \
  --file=docker/postgres/customer-role-grants.sql
```

Configure `CustomerDb` with the runtime credential and
`CustomerRetentionDb` with the retention credential. Never use the migrator
credential for API runtime.

## Retention activation

1. Start with `WorkerEnabled=true` and `DryRun=true`.
2. Observe candidate count and processing duration.
3. Approve hard purge versus archive and legal-hold behavior.
4. Verify runtime cannot update/delete audit or execute purge.
5. Verify retention can execute purge but cannot directly delete tables.
6. Configure `CustomerRetentionDb` outside source control.
7. Set `DryRun=false` and monitor failures and purged count.

The purge function uses database time and accepts only a bounded batch size. If
retention fails, return to dry-run; do not grant broader table permissions.

## Migration and recovery

Production migrations run as a controlled deployment job. Do not enable API
startup migration in production. Capture a restorable backup, apply migrations,
then verify readiness, Customer create/query, audit append, and role permissions.
Prefer forward-fix after a migration has committed.

Plaintext Customer documents are sensitive. Backups, exports, support queries,
and logs must not copy document values to unprotected destinations.
