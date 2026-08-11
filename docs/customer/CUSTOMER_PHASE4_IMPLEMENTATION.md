# Customer Phase 4 Implementation

## Scope

Phase 4 simplifies Citizen documents to normalized plaintext at rest while
preserving Customer ownership, masked APIs, PII-safe logs, PostgreSQL
uniqueness, optimistic concurrency, and atomic audit behavior.

The unpublished Customer migration history was rebased to a single
`CustomerBaselinePlaintext` migration because no Customer migration had been
applied to the target database and no Customer data existed.

## Data contract

- Vietnam Citizen ID is normalized to exactly 12 digits and country `VN`.
- Passport is normalized to uppercase alphanumeric text, 6 to 20 characters,
  and scoped by ISO alpha-2 issuing country.
- The database unique key is `(CitizenDocumentType,
  CitizenIssuingCountryCode, CitizenDocumentNumber)`.
- Raw document values remain excluded from API responses, logs, traces, and
  audit change payloads.

## Retention

Audit retention defaults to 365 days. The hosted worker is disabled by default
and Docker enables it in dry-run mode. Configuration also controls batch size
and interval.

PostgreSQL enforces audit append-only behavior with a trigger. Expired rows can
only be removed through `customer.purge_expired_customer_audits`, which validates
the batch size, uses PostgreSQL `clock_timestamp()` as the trusted cutoff, and
selects rows using `FOR UPDATE SKIP LOCKED`. Execute permission is revoked from
`PUBLIC` and granted only to the retention role.

Production activation still requires an approved archive-versus-purge policy
and separate runtime, migrator, and retention database credentials.

## Operations

- `/health/live` checks process liveness without dependencies.
- `/health/ready` checks Customer database connectivity.
- Retention exposes OpenTelemetry counters for purged records and failures.
- Purge uses a separate `CustomerRetentionDb` credential rather than the API
  runtime connection.
- Production migrations remain a controlled deployment step; automatic
  migration is intended only for explicitly configured environments.

Encryption, HMAC lookup, key rotation, audited plaintext PII-read APIs,
Customer-Identity linkage, and create idempotency remain deferred.

Database grants are provisioned with
`docker/postgres/customer-role-grants.sql`. Production retention remains dry-run
until archive versus purge and legal-hold policy are approved.
