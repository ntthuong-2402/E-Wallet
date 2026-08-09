# Coding and verification checklist

## Before coding

- Locate the real solution/project and nearest instructions.
- Identify bounded context, aggregate, transaction boundary, and data owner.
- Write acceptance criteria including negative and duplicate cases.
- Decide API/event compatibility and migration strategy.
- Decide authorization, audit, idempotency, retry, timeout, and concurrency behavior.
- Record unresolved financial rules in an ADR/open-decision document.

## C# implementation

- Enable nullable reference types.
- Use async I/O and propagate `CancellationToken`.
- Use `TimeProvider` or an injectable clock.
- Use value objects for money/currency when beneficial.
- Configure EF Core numeric precision explicitly.
- Do not return persistence entities from APIs.
- Keep endpoints thin and domain behavior testable without infrastructure.
- Avoid generic repository wrappers with no semantic value.
- Use explicit transaction boundaries.
- Insert outbox records in the same transaction as state changes.
- Use unique constraints as the final guard for idempotency.
- Use structured message templates and mask sensitive data.

## Database review

Check:
- primary/unique keys and query-driven indexes;
- same-context foreign keys only;
- check constraints for non-negative amounts and debit/credit shape where practical;
- version/concurrency token;
- timestamp and decimal types;
- append-only enforcement for posted journals;
- migration downgrade/forward-fix plan;
- query plan for hot paths;
- retention and partition/archive strategy for audit, idempotency, inbox/outbox, and status history.

## Mandatory behavior tests

Identity/security:
- login success/failure/lockout;
- refresh rotation and old-token reuse;
- missing permission and resource ownership denial;
- invalid/expired token;
- invalid signature, stale timestamp, and replayed nonce;
- exact CORS/rate-limit behavior where applicable.

Payment/ledger:
- insufficient balance;
- same idempotency key with same payload;
- same key with different payload;
- duplicate RabbitMQ delivery;
- invalid state transition;
- unbalanced or cross-currency journal rejection;
- provider definite rejection;
- provider timeout/unknown outcome and reconciliation;
- cancellation eligibility;
- refund creates a new operation;
- reversal creates compensating entries;
- concurrent debit from one account;
- notification failure does not roll back payment.

Delivery:
- migration from previous schema;
- clean database migration;
- backup/restore rehearsal;
- Docker build and non-root runtime;
- health live/ready behavior;
- smoke test after deployment.

## Evidence format

For every verification report include:
- exact command;
- working directory and relevant environment assumptions;
- exit code/result;
- concise important output;
- tests skipped and reason;
- blocker versus product defect distinction;
- final `PASS`, `FAIL`, or `UNKNOWN` per gate.
