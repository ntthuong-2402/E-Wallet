# Transaction API

Version: 1.0
Date: 2026-08-12

The Transaction API belongs to the `PaymentLedger` bounded context. It supports
operator-driven VND internal transfers and full reversal. PaymentLedger owns
ledger accounts, journals, entries, and the authoritative available balance.

All routes require a valid bearer token and the endpoint permission. Responses
created by Friday use the common `{ code, message, data, traceId }` envelope.
Framework-generated 400/401/403/429 bodies are not guaranteed to use it.

## Permissions

| Permission | Use |
|---|---|
| `LEDGER_ACCOUNTS_CREATE` | Open a zero-balance ledger account |
| `LEDGER_ACCOUNTS_READ` | Read account and authoritative balance |
| `TRANSACTIONS_TRANSFER_CREATE` | Post an internal transfer |
| `TRANSACTIONS_READ` | Read and search transactions |
| `TRANSACTIONS_REVERSE` | Reverse a posted internal transfer |

## Idempotency

Write requests require `refId`: 1-100 case-sensitive safe ASCII characters
(`A-Z`, `a-z`, digits, `.`, `_`, `:`, `-`). Uniqueness is scoped by
authenticated actor plus operation. Repeating the same normalized request
returns the original response. Reusing the same scoped `refId` for a different
payload returns HTTP 409 `LEDGER_REF_ID_CONFLICT`.

## Open ledger account

```http
POST /api/ledger/accounts
```

```json
{
  "refId": "account:open:0001",
  "accountRef": "CUSTOMER:CUS_01JY7RM8G4WY6P2KQ3BN"
}
```

Accounts open active with currency `VND` and `availableBalance` equal to zero.
Funding/cash-in is outside this MVP.

## Read ledger account

```http
GET /api/ledger/accounts/{accountId}
```

The response includes `id`, `accountRef`, `currency`, `status`,
`availableBalance`, `version`, and UTC timestamps.

## Post internal transfer

```http
POST /api/transactions/transfers
```

```json
{
  "refId": "transfer:20260812:0001",
  "sourceAccountId": "11111111-1111-1111-1111-111111111111",
  "destinationAccountId": "22222222-2222-2222-2222-222222222222",
  "amount": 250000,
  "currency": "VND",
  "description": "Internal transfer"
}
```

Rules:

- amount is a positive whole VND amount;
- source and destination differ and are active;
- source must have sufficient authoritative available balance;
- accounts are locked by ascending ID before funds validation;
- the transfer, balance changes, journal, entries, and idempotency result commit
  atomically;
- an append-only success audit records actor, refId, transaction ID and trace ID
  without copying the free-form description;
- the posted journal contains one equal debit and credit entry.

## Reverse transfer

```http
POST /api/transactions/{transactionId}/reversals
```

```json
{
  "refId": "reversal:20260812:0001",
  "reason": "Confirmed duplicate business transfer"
}
```

Only an unreversed posted internal transfer is eligible. Full reversal creates
a new compensating transaction and journal. The original posted journal is
never modified. Reversal fails if the original beneficiary no longer has
enough available balance.

## Read and search

```http
GET /api/transactions/{transactionId}
GET /api/transactions/by-ref/{refId}?operation=CREATE_INTERNAL_TRANSFER
GET /api/transactions?accountId={id}&currency=VND&type=InternalTransfer&status=Posted&page=1&pageSize=50
```

For reversal lookup use `operation=REVERSE_INTERNAL_TRANSFER`. Search accepts
UTC `postedFromUtc`/`postedToUtc`, caps `pageSize` at 100, and returns pagination
metadata in `X-Total-Count`, `X-Page`, and `X-Page-Size`.

## Application error codes

| HTTP | Code | Meaning |
|---:|---|---|
| 400 | `LEDGER_INVALID_TRANSFER` | Invalid currency, amount, or account combination |
| 404 | `LEDGER_ACCOUNT_NOT_FOUND` | Ledger account does not exist |
| 404 | `LEDGER_TRANSACTION_NOT_FOUND` | Transaction/reference does not exist |
| 409 | `LEDGER_ACCOUNT_REF_CONFLICT` | Account reference already exists |
| 409 | `LEDGER_ACCOUNT_INACTIVE` | An account cannot post |
| 409 | `LEDGER_INSUFFICIENT_FUNDS` | Debit/reversal would overdraw an account |
| 409 | `LEDGER_INVALID_REVERSAL` | Transfer is not eligible or was already reversed |
| 409 | `LEDGER_REF_ID_CONFLICT` | Scoped refId was used with another payload |
| 409 | `LEDGER_CONCURRENCY_CONFLICT` | Persisted account state changed concurrently |

## Deferred capabilities

Funding/cash-in/out, holds, external providers, unknown provider outcomes,
refund, cancellation, partial reversal, foreign exchange, self-service account
ownership, RabbitMQ outbox/inbox, and reconciliation APIs are not part of v1.
