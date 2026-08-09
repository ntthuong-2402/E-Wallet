# Test Matrix

Unit: domain state machine, ledger balancing, fee/limit rules, error mapping.
Integration: PostgreSQL migrations/constraints/transactions/outbox, RabbitMQ publish-consume/inbox, Redis/Valkey fallback, auth endpoints.
Contract: OpenAPI/API contracts, event schema/version compatibility, provider adapter contracts.
E2E: login -> payment -> ledger -> notification/webhook; duplicate request; timeout/recovery; refund/reversal.
Security: unauthorized/forbidden, expired token, invalid signature, replay nonce, rate limit, injection/over-posting, sensitive-log review.
Performance: balance read, payment acceptance, same-account contention, duplicate workload, provider outage, queue backlog/recovery.

QA gate: build + critical unit/integration + migration + security high/critical policy + image build + smoke + known issues/test evidence.
