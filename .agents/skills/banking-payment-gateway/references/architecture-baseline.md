# Architecture baseline

## Deployment baseline

Start with six deployable units:
- Gateway.Api
- Identity.Api
- CustomerAccount.Api
- PaymentLedger.Api
- Integration.Worker
- Notification.Worker

The unit count is a ceiling for the initial implementation, not a target that must be reached before useful software exists. A modular monolith or fewer units is acceptable during the earliest skeleton if boundaries remain explicit.

## Data ownership

| Context | Owns | Must not own |
|---|---|---|
| Gateway | routes, edge policies, correlation, request limits | business data, repositories, ledger logic |
| Identity | users, roles, permissions, OAuth/OIDC clients, refresh tokens, MFA, credential lifecycle | customer accounts, payment state, journal data |
| Partner module | partner business profile/status, non-secret integration policy | raw credential secrets, payment posting |
| CustomerAccount | customer, account lifecycle, beneficiary, limits configuration, projected balance view | posted ledger/journal source of truth |
| Payments | payment order, state history, idempotency, orchestration, provider attempts | direct mutation of posted journals |
| Ledger | ledger accounts, reservations, journals, journal lines, authoritative balances/snapshots | partner credentials, notification delivery |
| Integration | provider adapters, mapping, provider request metadata, reconciliation queries | authoritative payment/ledger state |
| Notification | webhook endpoints, delivery attempts, notification templates/history | determining payment success |

Cross-service IDs are application references only. Database foreign keys stop at the bounded-context boundary.

## Correct payment orchestration

A safe external-transfer outline is:
1. Accept and durably deduplicate payment request.
2. Persist payment plus outbox in one transaction and return 202 when asynchronous.
3. Payment process manager requests funds reservation.
4. Ledger reserves funds exactly once and publishes result.
5. Only after reservation success does the process manager request provider execution.
6. Integration calls the provider with a stable provider idempotency/reference key.
7. A definite provider result advances the workflow.
8. A timeout/transport break enters `UNKNOWN` or reconciliation-required handling; do not blindly declare failure or release funds.
9. On definite success, post/settle according to the ledger model.
10. On definite rejection, release reservation or compensate using a new ledger operation.
11. Notification/webhook runs independently and cannot roll back a completed payment.

Internal transfers can skip the external provider and post the internal debit/credit atomically within the Ledger boundary when the model allows it.

## Balance model

- Ledger owns authoritative balance derivation.
- `balance_snapshots` accelerate reads but are derived from journal/reservation sequence.
- A CustomerAccount balance table, when present, is a projection/read model and must expose staleness metadata (`asOfUtc`, sequence/version).
- Never update an account balance independently of ledger posting/reservation rules.

## Technology baseline

- Prefer .NET 10 LTS for new source; .NET 8 is an explicit compatibility exception.
- PostgreSQL is the only guaranteed database in the first three phases.
- Valkey is the default permissive cache option. Redis may be used after version/license review.
- Linux runtime containers are canonical.
- The project is production-like, not certified or guaranteed for real bank production.
