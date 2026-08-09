# Financial Invariants Checklist

- Money is decimal/numeric with explicit precision/scale.
- Currency is explicit.
- Posted journal is immutable; correction is reversal + new entry.
- Debit total equals credit total.
- Payment transitions are state-machine controlled and concurrency protected.
- Durable idempotency unique key includes the appropriate client/partner + endpoint + key; store request hash.
- Same idempotency key with different payload is rejected.
- Consumer redelivery must not duplicate posting.
- Same-account concurrent debits must not overspend because of a race.
- Provider timeout/connection loss is an unknown outcome until reconciled according to provider contract.
- Cancel, refund, and reversal are separate operations with explicit eligibility/state/ledger effects.
- Balance projections outside Ledger are read models, not financial authority.
