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
