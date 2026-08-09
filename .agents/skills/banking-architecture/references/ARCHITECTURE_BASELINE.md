# Architecture Baseline

Initial deployable units:
1. Gateway.Api
2. Identity.Api
3. CustomerAccount.Api
4. PaymentLedger.Api
5. Integration.Worker
6. Notification.Worker

Guiding principles:
- bounded contexts over table-based services;
- one primary database technology initially (PostgreSQL), with logical ownership per service;
- no direct cross-service table access;
- local ACID + eventual consistency across services;
- outbox/inbox for reliable messaging;
- OpenTelemetry from the beginning;
- Linux containers as production-like default;
- avoid premature service extraction for QR, Ledger, Audit, Webhook, or Reconciliation until lifecycle/scale/team needs justify it.
