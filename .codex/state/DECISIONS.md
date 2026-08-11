# Durable Decisions

Keep only stable decisions that materially affect future implementation. Use an ADR for detailed rationale once a decision becomes significant.

## D-001 — Controlled service decomposition

Status: Accepted

- Begin with approximately six coarse-grained deployable units: Gateway, Identity, CustomerAccount, PaymentLedger, Integration Worker, Notification Worker.
- Do not split services by table or trivial API.

## D-002 — Primary database

Status: Accepted

- PostgreSQL is the primary project database.
- Oracle/SQL Server may be integration adapters or later experiments, not simultaneous first-class MVP targets.

## D-003 — Financial source of truth

Status: Accepted

- Ledger is authoritative for financial postings.
- Redis/Valkey is never the source of truth for balances, payments, idempotency, or audit evidence.

## D-004 — Distributed consistency

Status: Accepted

- Use local ACID transactions inside a service.
- Use outbox/inbox plus Saga/process-manager patterns across services; do not default to 2PC.

## D-005 — Context persistence

Status: Accepted

- Enable Codex Memories for eligible sessions.
- Keep deterministic task checkpoints in `.codex/state/` so work can recover after compaction/new chats.
- Exclude external-context threads from automatic memory generation by default for this banking-oriented project.

## D-006 - Customer bounded context ownership

Status: Accepted

- Customer remains inside the Friday modular monolith initially but owns a
  dedicated `CustomerDbContext`, `customer` schema, migrations, transaction
  boundary, repositories, and contracts.
- Customer does not query Admin/Identity tables and creates no cross-context
  foreign keys. Actor and future login identities are external identifiers.
- `CustomerCode` is required, unique, immutable, and generated from a secure
  random value by the system.
- `Closed` is a terminal Customer state.
- The initial Customer states are `Active`, `Suspended`, and `Closed`; new
  Customers start `Active`.
- `CitizenId` is required and unique and supports Vietnam CCCD plus
  country-scoped passports. For the current basic-feature stage, normalized
  document numbers are stored as plaintext and database uniqueness uses type,
  issuing country, and normalized number. Encryption and keyed lookup are
  explicitly deferred; API responses, logs, traces, and audit payloads must
  still avoid raw PII.
- Customer change audit is append-only and commits atomically with successful
  Customer mutations; raw PII must not enter logs or audit payloads. Audit PII
  is masked and retention defaults to a configurable 365 days.
- Initial Create Customer API idempotency is deferred.
- Customer `FullName` is required; `DateOfBirth` remains optional.
- Phase 3 APIs expose a masked display name and masked CitizenId, and omit
  DateOfBirth. `CUSTOMERS_PII_READ` is reserved for a later dedicated and
  audited plaintext-read use case.
- Modules contribute permission codes through a generic BuildingBlocks
  contract; Admin retains ownership of permission persistence and bootstrap.
- Detailed rationale and pending policy decisions are recorded in
  `docs/adr/ADR-0001-customer-bounded-context-persistence.md` and
  `docs/customer/CUSTOMER_PHASE0_DESIGN.md`.
