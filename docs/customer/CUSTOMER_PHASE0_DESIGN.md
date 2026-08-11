# Customer Phase 0 Design

> Historical design record. The AES/HMAC decision was superseded by the
> approved Phase 4 plaintext-at-rest decision. See
> `CUSTOMER_PHASE4_IMPLEMENTATION.md` for current behavior.

Status: Approved; Phase 2 policy gates resolved
Date: 2026-08-11

## 1. Scope

This document defines the design gate for the first Customer module delivery.
It does not create production projects, APIs, database objects, or migrations.

Initial capabilities:

- Create Customer;
- Get Customer by internal ID;
- Get Customer by `CustomerCode`;
- bounded search and pagination;
- update Customer profile;
- change Customer status;
- audit Customer changes.

## 2. Ownership and actors

Customer owns profile/PII, `CustomerCode`, lifecycle status/open date,
optimistic concurrency state, and Customer change audit records.

Admin/Identity owns operator login, JWTs, sessions, roles, permissions, and
future customer login credentials.

An authenticated operator is the actor for the initial APIs. Customer audit
stores the actor's external user ID and trace ID, with no Admin foreign key.

## 3. Approved aggregate direction

```text
Customer
  Id                 internal persistence identity
  CustomerCode       required, unique, immutable, system-generated
  FullName           required
  DateOfBirth        optional until requiredness is approved
  CitizenId          required and unique
  Status             required
  OpenedOnUtc        required under the proposed initial-active flow
  Version            required optimistic concurrency token
  CreatedOnUtc       required UTC timestamp
  UpdatedOnUtc       required UTC timestamp
```

`FullName` is required. `CustomerCode` and `CitizenId` are approved as required and unique.
System-maintained lifecycle and persistence fields remain required for
correctness. There is no delete operation. A closed Customer is retained.

## 4. CustomerCode

`CustomerCode` is system-generated and is not accepted from client input.

```text
CUS_<CROCKFORD_BASE32_RANDOM>
```

Example: `CUS_01JY7RM8G4WY6P2KQ3BN`.

Rules:

- cryptographically secure random bytes;
- Crockford Base32 alphabet;
- invariant uppercase canonical representation;
- unique database constraint;
- immutable after creation;
- bounded retry on unique-conflict collision;
- at least 80 bits of randomness;
- no time-only or sequential predictable fallback.

The exact random length is finalized and tested in Phase 1.

## 5. Status state machine

The initial state set is Accepted:

```text
Active -----> Suspended
  |               |
  |               +-----> Active
  |               |
  +---------------+-----> Closed

Closed -X-> any state
```

Accepted invariant: `Closed` is terminal and can never be reopened.

Accepted rules:

- Customer is created as `Active`;
- `OpenedOnUtc` is assigned at creation and immutable;
- `reason` is mandatory for suspend, reactivate, and close;
- only aggregate transition methods change status;
- generic profile update cannot modify status;
- stale concurrency version causes conflict without mutation.

`Active`, `Suspended`, and `Closed` are the complete initial state set, and a
Customer is initially created as `Active`.

## 6. API and authorization draft

```text
POST   /api/customers
GET    /api/customers/{id}
GET    /api/customers/by-code/{customerCode}
GET    /api/customers
PUT    /api/customers/{id}
PATCH  /api/customers/{id}/status
GET    /api/customers/{id}/audit
```

```text
CUSTOMERS_CREATE
CUSTOMERS_READ
CUSTOMERS_PII_READ
CUSTOMERS_UPDATE
CUSTOMERS_STATUS_CHANGE
CUSTOMERS_AUDIT_READ
```

Rules:

- deny by default;
- create/update cannot set system, status, audit, or identity-link fields;
- list DTOs never expose a full `CitizenId`;
- pagination is required and bounded, proposed maximum 100;
- mutable commands carry a concurrency version;
- Create API idempotency is deferred.

## 7. Search draft

Initial filters: `search`, `customerCode`, `status`, `openedFrom`, `openedTo`,
`page`, `pageSize`, `sortBy`, and `sortDirection`.

- Ordering is deterministic with `Id` as tie breaker.
- Sort fields are allow-listed.
- Reads are no-tracking and project to DTOs.
- No unbounded list endpoint exists.
- PII search values never enter logs.
- Indexes follow the approved query contract and PostgreSQL query-plan evidence.

## 8. Audit contract

Customer audit is append-only and owned by Customer.

```text
EventId, CustomerId, CustomerCode, EventType, ActorUserId,
ChangedFields, FromStatus, ToStatus, Reason, Outcome, TraceId, OccurredOnUtc
```

Important audited fields include `CustomerCode`, `OpenedOnUtc`, `CitizenId`,
`DateOfBirth`, `FullName`, `Status`, and future identity linkage.

Audit coverage does not imply plaintext storage:

- CustomerCode, timestamps, and status may be stored in full;
- CitizenId, date of birth, and full name must not appear in logs;
- audit change payloads use masking or field-level encryption according to the
  approved audit-access requirement;
- successful mutation and audit append share one Customer transaction;
- failed audit write rolls back the mutation;
- no audit update/delete API exists.

Accepted `CUST-PENDING-002`: audit stores masked PII rather than full historical
CitizenId, date-of-birth, or full-name values.

Accepted `CUST-PENDING-003`: audit retention defaults to 365 days and is
configuration-driven. Phase 2 records `RetainUntilUtc`; an archive/purge job is
not yet implemented.

## 9. CitizenId policy

`CitizenId` is sensitive PII. Its requiredness and uniqueness are Accepted:

- required;
- unique;

Accepted `CUST-PENDING-004` implementation policy:

- Vietnam Citizen ID (CCCD) uses country `VN` and exactly 12 digits;
- Passport supports an ISO alpha-2 issuing country and 6-20 letters/digits;
- permitted display separators are removed before canonical comparison;
- AES-256-GCM protects the canonical document number;
- keyed HMAC-SHA256 provides country/type-scoped uniqueness and exact lookup;
- only a masked suffix is available for normal display and audit.

The Phase 2 schema enforces requiredness and uniqueness without storing the
canonical document number in plaintext.

## 10. Future Identity linkage

The future link is Accepted in principle:

- Identity owns credentials and sessions;
- Customer stores only an external identity identifier;
- no database foreign key crosses contexts;
- link/unlink is a dedicated authorized and audited use case;
- generic Customer update cannot modify the link;
- no unused identity column is added before a concrete flow is approved;
- future service synchronization uses explicit contracts and outbox/inbox when
  durable messaging is available.

Pending `CUST-PENDING-005`: approve one-to-one cardinality and link/unlink
authority. This may remain pending while linkage is outside initial scope.

## 11. Phase 1 entry gate

Architecture scaffolding and all Phase 2 schema policies are approved.

## 12. Phase 0 acceptance checklist

- [x] Customer ownership documented.
- [x] Dedicated DbContext/schema/migrations Accepted.
- [x] No cross-context SQL/FK rule documented.
- [x] Random immutable CustomerCode direction Accepted.
- [x] Closed-is-terminal invariant Accepted.
- [x] Future Identity linkage direction Accepted.
- [x] Create API idempotency deferred.
- [x] API and permission contract drafted.
- [x] PII/audit exposure risks documented.
- [x] Initial status set approved.
- [x] CitizenId requiredness and uniqueness approved.
- [x] CitizenId normalization/protection approved.
- [x] Audit PII visibility and retention approved.
