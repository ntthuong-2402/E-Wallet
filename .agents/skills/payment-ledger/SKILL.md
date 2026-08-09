---
name: payment-ledger
description: Use for payment orchestration, idempotency, account holds/reservations, double-entry posting, balance correctness, refund/reversal, provider outcome handling, and concurrent debit safety.
---


# Payment and Ledger Workflow

Before changing payment/ledger code, identify:
- current and allowed next payment states;
- authoritative balance/ledger data;
- idempotency key and unique constraints;
- local transaction boundary;
- outbox/inbox behavior;
- duplicate/retry behavior;
- same-account concurrency behavior;
- provider timeout/unknown-outcome behavior;
- reversal/refund/cancel distinction.

Required evidence for financial changes usually includes negative tests, duplicate-delivery tests, and a concurrency test when balance can change.

Read `references/FINANCIAL_INVARIANTS.md` for the detailed checklist.

