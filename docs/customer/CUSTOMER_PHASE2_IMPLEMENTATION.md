# Customer Phase 2 Implementation

> Historical implementation record. Encryption and keyed lookup were removed
> before the unpublished Customer migrations were deployed. See Phase 4 for
> current behavior.

Status: Implemented
Date: 2026-08-11

## Delivered boundary

- Customer aggregate with `Active`, `Suspended`, and terminal `Closed` state.
- Immutable system-generated `CustomerCode` with 80 random bits.
- Vietnam CCCD and country-scoped Passport document types.
- AES-256-GCM document encryption and keyed HMAC-SHA256 exact lookup.
- Masked document display value; plaintext CitizenId is not persisted.
- Optimistic concurrency token on Customer profile mutations.
- Append-only Customer change audit model with configurable retention date.
- Customer repositories, EF configurations, constraints, indexes, and migration.

## Citizen document policy

Vietnam CCCD canonical form is exactly 12 digits and issuing country `VN`.
Passport canonical form is 6-20 uppercase letters/digits and requires an ISO
alpha-2 issuing country. Spaces and hyphens are accepted as input separators.

The database stores:

- randomized AES-GCM ciphertext;
- HMAC-SHA256 lookup hash scoped by format version, type, and country;
- masked value containing only the final four characters.

Encryption and lookup keys are independent Base64-encoded 32-byte secrets.
They are supplied by configuration/environment and no real key is committed.

## Audit retention

`Customer:AuditRetention:RetentionDays` defaults to 365. Each audit row records
`RetainUntilUtc`, allowing policy changes for later records. Phase 2 does not
implement destructive purge/archive behavior.

Audit `ChangedFieldsJson` must contain masked values only. `CustomerDbContext`
rejects update or delete state for tracked Customer audit rows.

## Persistence

Migration `CustomerProfileAndAudit` creates:

- `customer.customers`;
- `customer.customer_change_audits`;
- unique CustomerCode and protected-document indexes;
- status/document/country constraints;
- Customer-local audit foreign key with restricted delete behavior.

## Deferred

- Customer commands, queries, DTOs, Minimal API endpoints, and permissions.
- Audit reader and retention/archive worker.
- Future Identity link/unlink behavior.
