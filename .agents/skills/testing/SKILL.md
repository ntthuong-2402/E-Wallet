---
name: testing
description: Use for unit, integration, architecture, contract, end-to-end, security, migration, smoke, and performance testing plus release-to-QA quality gates.
---


# Testing Workflow

Select the smallest test layer that proves the behavior, then add broader tests only where boundaries require them.

Financial critical paths should cover:
- success and rejection;
- invalid state transition;
- idempotency same-key/same-payload and same-key/different-payload;
- duplicate message delivery;
- debit/credit imbalance rejection;
- provider timeout and recovery;
- refund/reversal;
- concurrent debit of the same account;
- migration upgrade;
- authorization/security negatives.

Prefer real database/RabbitMQ containers for integration tests over pretending infrastructure behavior with mocks.

Read `references/TEST_MATRIX.md` for the project baseline.

