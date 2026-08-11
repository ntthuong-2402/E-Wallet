# Customer Phase 3 Implementation

> Historical implementation record. Ciphertext, lookup hash, and key-rotation
> references were superseded by Phase 4 plaintext-at-rest persistence.

Status: Implemented and PostgreSQL verified
Date: 2026-08-11

## Delivered use cases

- Create Customer with generated CustomerCode and masked atomic audit.
- Get Customer by ID or CustomerCode.
- Bounded Customer list with filters, deterministic ordering, and pagination.
- Update Customer profile with optional document replacement.
- Change Customer lifecycle status with expected-version concurrency checks.
- Read bounded Customer audit history.

All responses expose only masked display name and masked document value;
DateOfBirth is not returned. Ciphertext, lookup hash, and plaintext CitizenId
are never returned. `CUSTOMERS_PII_READ` is reserved for a future dedicated
audited PII-read use case.

## Authorization

Customer contributes six permission codes through the cross-cutting
`IPermissionContribution` contract. Admin remains the owner of permission
persistence and bootstrap. The permission policy provider and SUPER_ADMIN
bootstrap consume contributions without Customer referencing Admin.

## Command transaction

Customer commands implement `ICustomerCommand<T>`. Create uses a Customer-local
flush inside the already-open transaction to obtain its database ID, then
appends the audit row. No early commit occurs; the final CustomerUnitOfWork
commit or rollback covers both records.

## API

```text
POST   /api/customers
GET    /api/customers/{id}
GET    /api/customers/by-code/{customerCode}
GET    /api/customers
PUT    /api/customers/{id}
PATCH  /api/customers/{id}/status
GET    /api/customers/{id}/audit
```

Endpoints are thin Minimal API adapters and enforce a named Customer permission.
Read and mutation endpoints also use per-subject/IP rate-limit partitions.

## Persistence delta

`CustomerPhaseThreeQueries` makes FullName required and adds indexes for opened
date/status pagination. The migration fails fast when existing rows require
FullName remediation rather than silently replacing missing names with an
empty/default value.

## Verification

- Customer unit/model/boundary tests: 20 passed.
- Customer API/security integration tests: 3 passed.
- Complete API integration suite: 10 passed.
- Admin unit tests: 4 passed after permission contribution changes.
- EF reports no pending Customer model changes and discovers three migrations.
- Security review confirmed named authorization/actor/over-posting boundaries;
  responses were hardened to masked name/document only, independent crypto keys
  are enforced, and Customer endpoints use partitioned rate limits.

The API integration fixture uses EF InMemory. A separate PostgreSQL verification
project creates a uniquely named clean database, applies all Customer migrations,
verifies relational invariants and query plans, and drops the database in a
`finally` cleanup.

Verified on PostgreSQL:

- all three migrations apply to an empty database;
- CCCD and Passport uniqueness constraints reject duplicates;
- concurrent same-document inserts allow exactly one writer;
- concurrent same-version updates allow exactly one writer;
- a failed audit insert rolls back the preceding Customer insert;
- CustomerCode and status/opened-date queries select their expected indexes
  under plan verification.

Explicit command:

```text
dotnet test tests/Friday.Modules.Customer.PostgreSqlTests/Friday.Modules.Customer.PostgreSqlTests.csproj --no-restore
```

## Deferred

- Create Customer idempotency.
- Full PII read endpoint and read auditing.
- Key rotation and re-encryption.
- Retention archive/purge worker.
- Future Identity link/unlink.
