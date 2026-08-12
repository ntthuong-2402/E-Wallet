# ADR-0001 - Customer bounded context and persistence ownership

> Amendments: the earlier AES/HMAC proposal was superseded before deployment.
> Phase 4 stores normalized document numbers as plaintext while restricting DB
> access and keeping raw PII out of APIs, logs, traces, and audit payloads.
> Customer create now requires a durable `refId`, and the approved account
> linkage is an external login-account reference without a cross-context FK.

Status: Accepted
Date: 2026-08-11

## Context

Friday is currently deployed as a modular monolith. Admin is the persisted
reference module, but it uses the shared `FridayDbContext` and shared migration
history. Repeating that persistence coupling for Customer would make ownership
unclear and make later extraction into `CustomerAccount.Api` more expensive.

Customer owns customer profile data, lifecycle status, and change audit
records. Admin/Identity owns authentication, authorization, credentials, roles,
permissions, and sessions. An operator may act on a Customer, but neither
context owns the other context's entities.

## Decision

1. Customer is a bounded context inside the Friday modular monolith.
2. Customer initially runs in `Friday.API` and uses the existing PostgreSQL
   deployment.
3. Customer owns a dedicated `CustomerDbContext`, `customer` schema, EF Core
   migration assembly/history, transaction boundary, repositories, and data
   contracts from its first implementation.
4. Customer code must not query Admin tables or use Admin repositories or
   persistence entities.
5. Customer creates no database foreign keys to Admin/Identity tables. Operator
   and future customer-login identities are external identifiers.
6. Customer mutations and Customer audit events commit in the same
   Customer-local transaction.
7. Customer does not own passwords, tokens, roles, or permissions.
8. Cross-context workflows use explicit contracts. When durable messaging is
   introduced, publication uses outbox/inbox rather than distributed
   transactions.
9. Create Customer requires a globally unique, case-sensitive `refId` persisted
   with the actor, normalized request hash, and original response snapshot.
   Same-reference concurrent requests are serialized in PostgreSQL; reuse by
   another actor or with another payload is rejected.
10. Customer may hold one active linkage to an external Identity/Admin login
    account, and an account may link to one active Customer. Link/unlink are
    dedicated permission-protected, audited operations; no cross-context FK is
    created.

## Planned project boundary

```text
Friday.API
  -> Friday.Modules.Customer.Application
  -> Friday.Modules.Customer.Infrastructure

Friday.Modules.Customer.Infrastructure
  -> Friday.Modules.Customer.Application
  -> Friday.Modules.Customer.Domain

Friday.Modules.Customer.Application
  -> Friday.Modules.Customer.Domain

Friday.Modules.Customer.Domain
  -> Friday.BuildingBlocks.Domain
```

The Customer module remains a library boundary, not a separately deployed
service in the initial implementation.

## Alternatives considered

### Continue using FridayDbContext

Rejected for Customer. It is the lowest-change Admin convention, but it would
share migrations, transaction registration, and physical model ownership with
unrelated contexts.

### Extract CustomerAccount.Api immediately

Rejected for now. A strong module and persistence boundary provides extraction
readiness without adding premature deployment and messaging cost.

### Add foreign keys to Admin/Identity

Rejected. Cross-context referential integrity would prevent independent
persistence evolution and later service extraction.

## Consequences

- The composition root registers multiple DbContexts and units of work.
  Customer commands must select the Customer transaction explicitly; DI
  registration order must not select it implicitly.
- Customer migrations require an unambiguous design-time configuration.
- Customer-local operations retain ACID consistency.
- Admin and Customer cannot depend on a cross-context relational transaction.
- Extraction into `CustomerAccount.Api` remains possible without moving Admin
  tables or rewriting Customer foreign keys.

## Verification / follow-up

Before Phase 1 is complete, verify:

- Customer commands resolve the Customer transaction behavior;
- migrations use only the `customer` schema and their own migration history;
- architecture tests reject Customer dependencies on Admin Infrastructure;
- no Customer-to-Admin database foreign key exists;
- a clean PostgreSQL database can apply Customer migrations independently.

Approved and pending business rules are maintained in
`docs/customer/CUSTOMER_PHASE0_DESIGN.md`.
