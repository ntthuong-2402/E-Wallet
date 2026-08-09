# Banking Payment Gateway — Codex Project Instructions

## 1. Mission

Build and maintain a production-like Banking / Payment API Gateway in C# and ASP.NET Core for a small backend team. Prefer controlled, coarse-grained services over premature microservice fragmentation.

Target deployable units initially:
- Gateway.Api
- Identity.Api
- CustomerAccount.Api
- PaymentLedger.Api
- Integration.Worker
- Notification.Worker

Default platform direction:
- C# / ASP.NET Core
- PostgreSQL as primary project database
- RabbitMQ for asynchronous messaging
- Redis or Valkey only for cache, rate limiting, replay protection, and coordination
- Docker with Linux runtime images
- OpenTelemetry + Prometheus + Grafana + Loki + Tempo/Jaeger

## 2. Non-negotiable financial invariants

Never violate these rules unless an approved ADR explicitly changes them:

1. Money uses `decimal` / database `numeric`; never `float` or `double` for financial amounts.
2. Currency is explicit and validated on every monetary operation.
3. Persist timestamps in UTC.
4. Payment status changes follow an explicit state machine; do not assign arbitrary statuses.
5. Posted ledger journals are append-only. Never update or delete a posted journal.
6. Reversal creates compensating entries; it does not mutate the original posting.
7. Total debit must equal total credit for each posted journal entry.
8. Every retryable financial command must be idempotent.
9. Durable idempotency lives in the database. Redis/Valkey may accelerate lookup but is not the source of truth.
10. RabbitMQ delivery is treated as at-least-once; consumers must be idempotent and use inbox/deduplication.
11. Publish integration events with a transactional outbox when DB state and event publication must stay consistent.
12. Cross-service workflows use Saga/process-manager style coordination. Do not default to distributed 2PC.
13. External API calls must not hold a long-running database transaction open.
14. Provider timeout or network loss does not automatically mean business failure. Treat unknown outcomes explicitly and reconcile/query status before unsafe retry or fund release.
15. Concurrency around debiting the same account must have a documented strategy and a race-condition test proving no lost update or unintended overdraft.

## 3. Service and data boundaries

- A service owns its data and migrations.
- No service may directly query or update another service's tables.
- Do not create cross-service foreign keys. Store external IDs/references instead.
- Gateway contains routing/security/cross-cutting concerns, not payment business logic or repositories.
- Ledger is the authoritative financial record. Any account balance outside Ledger is a read model/snapshot and must carry freshness/version metadata.
- BuildingBlocks contains only genuinely cross-cutting code; never use it as a dumping ground for business domain logic.

## 4. Security baseline

- Deny by default and use least privilege.
- Validate authentication and authorization at service boundaries; do not trust client-supplied identity headers.
- Prefer local JWT validation using trusted signing keys/JWKS for normal JWTs rather than calling Identity on every request.
- Admin/operator flows require MFA when implemented.
- Partner APIs use scoped credentials plus HMAC signing and/or mTLS as designed.
- HMAC canonicalization must define exact bytes, encoding, path/query normalization, body hash, timestamp tolerance, nonce rules, key id, and constant-time signature comparison.
- Never log passwords, access/refresh tokens, private keys, client secrets, PIN, CVV, or unnecessary PII.
- Secrets never enter source control.
- Rate limit authentication and partner endpoints.
- Audit sensitive admin actions, credential rotation, permission failures, security events, and financial state transitions.

## 5. Context-management protocol

Treat model context as RAM and repository state as durable working memory.

### At the beginning of a task/session

1. Read this `AGENTS.md` and the nearest nested `AGENTS.md` for the working directory.
2. Read `.codex/state/CURRENT_TASK.md` if it exists and is relevant.
3. Read `.codex/state/DECISIONS.md` only for decisions relevant to the requested area.
4. Read `.codex/state/VERIFICATION.md` only when continuing verification/debugging.
5. Load only the skill needed for the current task. Do not preload every skill/reference.
6. Search first, then read targeted files/ranges. Do not recursively dump the repository into context.

### While working

- Prefer `rg`, symbol search, `git diff --stat`, and targeted file reads over broad file dumps.
- For files larger than roughly 500 lines, locate relevant symbols/sections before reading.
- For logs, save full output to a file and inspect errors/warnings/tails. Do not paste huge logs into the conversation.
- For tests, keep the full raw output outside model context; summarize only failing test names, error messages, and evidence needed to act.
- Use subagents for bounded exploration/review/testing so the main thread receives concise findings instead of all intermediate context.

### Save checkpoints

Update `.codex/state/CURRENT_TASK.md` at a stable milestone, before intentional compaction, before switching to a substantially different workstream, and before ending a long session.

A checkpoint must contain only:
- objective;
- bounded scope;
- current status;
- files changed/important symbols;
- decisions made for this task;
- verification completed;
- blockers/unknowns;
- exact next actions.

Do not append conversation transcripts. Rewrite stale sections so the file remains concise. Target <= 120 lines.

Update `.codex/state/DECISIONS.md` only for durable project decisions. If decisions become large, promote them to `docs/adr/` and keep only an index/summary here.

Update `.codex/state/VERIFICATION.md` with the latest verification state only. Store large logs under `.codex/state/logs/` rather than embedding them.

### Recover after compaction/new chat

Reconstruct state from:
1. Git status/diff.
2. `.codex/state/CURRENT_TASK.md`.
3. Relevant entries in `.codex/state/DECISIONS.md`.
4. `.codex/state/VERIFICATION.md`.
5. Targeted source files.

If repository state conflicts with an old conversation summary, trust the repository and current source unless the user explicitly says otherwise.

## 6. Skill routing

Use repository skills progressively:
- `banking-architecture`: service boundaries, decomposition, ADRs, data ownership.
- `payment-ledger`: payments, balances, holds, posting, reversal/refund, concurrency, idempotency.
- `identity-security`: login, OAuth/OIDC, roles/permissions, partner credentials, HMAC/mTLS, security hardening.
- `messaging-reliability`: RabbitMQ, outbox/inbox, retries, DLQ, Saga, webhook reliability.
- `database`: schema, migrations, indexes, EF Core/Dapper, query performance, PostgreSQL ownership.
- `testing`: unit/integration/contract/E2E/security/performance testing and QA gates.
- `deployment`: Docker, CI/CD, observability, release, rollback, runbooks.

Do not load a skill merely because it exists. Load it when the task matches its scope.

## 7. Coding rules

- Nullable reference types enabled.
- Async I/O end-to-end; no `.Result` or `.Wait()` in request paths.
- Propagate `CancellationToken` for I/O and request work where meaningful.
- Thin controllers/endpoints; business rules belong in domain/application code.
- DTOs/contracts are not persistence entities.
- Avoid meaningless generic repositories.
- Use explicit transaction boundaries.
- Do not `SELECT *` in important/hot queries.
- Avoid N+1 queries and unbounded collections.
- Use `AsNoTracking` for EF Core read-only queries when appropriate.
- Introduce Dapper/raw SQL only for measured needs, not premature abstraction.
- Structured logging only; include trace/correlation identifiers where useful.
- Prefer `TimeProvider` or a testable clock abstraction for time-dependent behavior.
- Make decimal precision/scale explicit in persistence mappings.
- Treat warnings as errors in CI where practical, with explicit documented exceptions.

## 8. Change workflow

Before editing:
1. Identify bounded context and owning service.
2. Search the existing implementation and tests.
3. Identify financial/security/data/message impact.
4. Read the nearest nested `AGENTS.md` and relevant skill.

During implementation:
- Make the smallest coherent change.
- Do not refactor unrelated areas unless required for correctness.
- Preserve backward compatibility for API/event/database contracts unless the task explicitly allows a breaking change.
- For schema changes, consider migration and rollback/forward-fix behavior.

After implementation:
1. Review `git diff --stat` and targeted diffs.
2. Build affected projects.
3. Run targeted unit tests.
4. Run relevant integration/architecture/contract tests when the change crosses those boundaries.
5. Check migration/security/logging impact.
6. Update `.codex/state/CURRENT_TASK.md` and `.codex/state/VERIFICATION.md`.

## 9. Required clarification flags

Call out rather than silently guess when the task depends on an unresolved business decision, especially:
- QR standard (internal vs EMVCo/VietQR/NAPAS or other profile);
- source of authoritative balance for a specific integration;
- provider timeout/unknown-result recovery contract;
- refund vs cancellation vs reversal eligibility;
- HMAC byte-level canonicalization;
- partner/profile ownership;
- same-account concurrency strategy;
- retention/regulatory requirements;
- production SLA/RTO/RPO that have not been sized or approved.

## 10. Definition of completion for code tasks

A code task is not complete solely because it compiles. Where relevant, completion includes:
- behavior implemented;
- tests covering critical path and negative/error path;
- idempotency/concurrency considered for financial commands;
- authorization/security considered;
- migrations/contracts/events updated;
- logs do not expose secrets/PII;
- targeted build/tests pass;
- checkpoint and verification state updated.

## Communication language

- Respond to the user in Vietnamese by default.
- Keep source code, identifiers, class names, method names, file names,
  API paths, commands, error messages, and technical terms in their
  original language when appropriate.
- Do not translate source code identifiers.
- Project knowledge files may use English unless the user explicitly
  requests Vietnamese documentation.
