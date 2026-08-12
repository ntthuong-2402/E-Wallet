# ADR-0002 - PaymentLedger ownership and internal-transfer MVP

Status: Accepted
Date: 2026-08-12

## Context

Friday needs basic financial transaction APIs. The word transaction already
describes the technical CQRS/EF transaction behavior, so the business boundary
must be explicit. Customer owns profiles and login-account linkage; Admin owns
identity and permissions. Neither context is the financial source of truth.

## Decision

1. The bounded context and module are named `PaymentLedger`; `Transaction` is
   an API/business resource inside that module.
2. PaymentLedger runs inside `Friday.API` initially and owns a dedicated
   `PaymentLedgerDbContext`, `payment_ledger` schema, migrations, repositories,
   keyed unit of work, ledger accounts, journals, entries, transactions, and
   authoritative balances.
3. The MVP supports VND whole-unit internal transfers only. Currency is always
   explicit and persisted with `numeric(19,0)` amounts.
4. Durable command idempotency is scoped by authenticated actor, operation,
   and case-sensitive `refId`. The normalized request hash and safe response
   snapshot are stored in the same local transaction as the posting.
5. Internal transfers lock both ledger-account rows in ascending account-ID
   order before checking funds and posting. Normal accounts cannot overdraft.
6. A transfer posts one immutable journal with equal debit and credit entries.
   The balance snapshot is updated in the same local transaction and is never
   authoritative independently of the ledger posting.
7. Reversal is a separate, permission-protected command. It creates a new
   compensating journal and never changes or deletes the original journal.
   Only one full reversal is allowed in the MVP.
8. Customer/Admin identifiers are external references only. PaymentLedger
   creates no cross-context foreign keys or cross-context SQL.
9. Initial APIs are operator-driven and require named permissions. The actor is
   derived from the validated JWT, never supplied by the request.
10. New ledger accounts open with zero balance. Funding, cash-in/out, provider
    integration, holds, refunds, cancellation, foreign exchange, and partial
    reversal are outside this MVP.

## Consequences

- `Friday.API` composes a fourth business module while remaining one deployable.
- PostgreSQL is required to prove advisory idempotency locks and account row
  locking; EF InMemory tests cannot establish concurrency correctness.
- Reverse transfer can fail for insufficient beneficiary balance because the
  MVP has no overdraft or reservation policy.
- Durable RabbitMQ publication is deferred. No in-process notification is
  described as an outbox or reliable integration event.
- Future self-service APIs need a separate approved account-ownership contract.

## Verification requirements

- clean and upgrade migration;
- same-key replay and changed-payload conflict;
- concurrent duplicate posting;
- same-source concurrent debit without overdraft or lost update;
- opposite-direction transfer without deadlock;
- balanced and append-only journals;
- compensating reversal and duplicate-reversal protection;
- authentication, permission, rate-limit, and over-posting negatives.
