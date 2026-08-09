# Clarified domain and security decisions

## Payment state semantics

Do not use one generic state list without transition ownership. At minimum distinguish:
- acceptance/validation state;
- funds reservation state;
- provider execution state;
- ledger posting/settlement state;
- reconciliation/unknown-outcome state;
- terminal business state.

A practical model may include `PENDING_REVIEW` or `RECONCILIATION_REQUIRED` in addition to the original states. A provider timeout cannot safely map directly to `FAILED` when the provider may have processed the request.

Each transition stores current version, actor/source, reason code, correlation/trace IDs, and occurred time. Protect transitions with an atomic compare-and-set or equivalent concurrency check.

## Cancellation, refund, reversal, chargeback

- **Cancellation**: stops an eligible payment before irreversible execution/posting.
- **Refund**: a new business payment returning value after an original successful payment.
- **Reversal**: accounting correction/undo represented by new compensating journal entries.
- **Chargeback**: dispute-driven process with separate rules; out of scope unless explicitly introduced.

Never update the original posted journal to implement any of these.

## Reservation and same-account concurrency

Optimistic concurrency alone is acceptable only if conflicts retry against fresh authoritative state and the workflow proves no overspend. For hot debit accounts consider a short database row lock, atomic conditional update, serialized account stream, or ledger posting function. Document lock order and timeout to avoid deadlocks.

Required tests:
- many concurrent debits just below/above available balance;
- duplicate command/message during contention;
- retry after optimistic conflict;
- release/settle reservation exactly once;
- no negative available balance unless an explicit overdraft facility exists.

## Idempotency

Durable uniqueness scope should normally be `(partnerId, endpoint/operation, idempotencyKey)`.

Persist:
- request hash based on stable canonical request bytes/fields;
- operation/payment ID;
- processing/result state;
- safe response reference or bounded response snapshot;
- timestamps and retention.

Same key plus different request hash is a conflict. Define behavior for a request arriving while the first is still processing. Protect stored response data according to PII retention requirements.

## Provider retry and reconciliation

Retry only when:
- the operation is read-only; or
- the provider guarantees idempotency using the same stable key/reference; or
- a prior attempt is proven not accepted.

For ambiguous timeouts:
- retain reservation according to a bounded policy;
- query provider status using the stable reference;
- reconcile asynchronously;
- raise an operational alert after the reconciliation deadline;
- release or reverse only after a definite outcome or approved manual process.

## JWT and token lifecycle

Gateway and services validate self-contained access tokens locally using issuer metadata/JWKS caching. Identity network calls on every request add an avoidable dependency and are needed only for introspection-style tokens or special revocation requirements.

Refresh-token rotation must detect reuse within a token family and revoke/flag the family when an old token reappears. Store only protected/hashed token material.

## Partner signing

The canonical signing specification must be test-vector driven. Define:
- UTF-8 encoding;
- uppercase method;
- normalized path and percent encoding;
- sorted query encoding and duplicate-key behavior;
- exact body bytes or canonical JSON rule;
- SHA-256 representation;
- timestamp format/unit and allowed skew;
- nonce length/charset/TTL;
- key ID and algorithm version;
- constant-time comparison;
- replay-store outage behavior.

## QR scope decision

Before implementation select one:
1. internal demo payload only;
2. EMVCo merchant-presented QR;
3. VietQR/NAPAS-compatible integration.

The choice changes fields, validation, checksum, interoperability, certification expectations, and test vectors. Do not label a custom QR payload “VietQR” or “bank standard.”
