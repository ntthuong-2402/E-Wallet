# Friday Customer API Integration Guide

Version: 1.0  
Updated: 2026-08-11  
Scope: current Customer endpoints implemented by `Friday.API`

## 1. Purpose

This document is the consumer-facing contract for Customer operations. It
describes the current routes, authentication and permissions, request and
response bodies, business rules, integration scenarios, HTTP statuses, and
application error codes.

Example base URL:

```text
https://api.example.com
```

All Customer routes are under `/api/customers`. All JSON property names use
`camelCase`. All timestamps are UTC ISO 8601 values.

The current API intentionally returns only masked Customer PII. It never returns
the raw Citizen ID/passport number or date of birth.

## 2. Common request headers

```http
Content-Type: application/json
Accept: application/json
Accept-Language: vi-VN
X-Correlation-Id: partner-request-000123
Authorization: Bearer <access-token>
```

- `Authorization` is required for every Customer endpoint.
- Obtain and rotate tokens through the Auth APIs documented in the
  [Friday Admin API Integration Guide](./ADMIN_API.md#6-auth-apis).
- The token must belong to a valid persisted session and an active, unlocked
  user that is not required to change their password.
- `Accept-Language` is optional. A message may be localized, so consumers must
  branch on `code`, never on `message`.
- `X-Correlation-Id` is optional. Use `traceId` from the response when reporting
  an error to the Friday API operator.
- Use TLS. Do not place access tokens or Customer PII in URLs, logs, metrics, or
  error reports.

## 3. Common response contract

### 3.1 Successful response

All currently implemented Customer operations, including create, return HTTP
`200 OK` with this envelope:

```json
{
  "code": "SUCCESS",
  "message": "Success",
  "data": {},
  "traceId": "4ac8d29ad9a74b81005b6b579cd5ee3f"
}
```

Create uses the message `Customer created.`. Other operations currently use
`Success`.

### 3.2 Application error response

Errors handled by the Friday application normally use the same envelope:

```json
{
  "code": "CUSTOMER_NOT_FOUND",
  "message": "Customer was not found.",
  "data": null,
  "traceId": "4ac8d29ad9a74b81005b6b579cd5ee3f"
}
```

Important transport exceptions:

- A missing, invalid, or expired JWT can produce a framework-generated `401`
  response without the application envelope.
- A valid user without the required permission can produce a
  framework-generated `403` response without the application envelope.
- Rate-limit rejection produces `429` and its body is not a stable application
  contract.
- Malformed JSON or transport/model-binding failure can produce ASP.NET Core
  `400 application/problem+json` instead of the application envelope.

Consumers must therefore check the HTTP status first, then parse `code` only
when the body is the Friday response envelope.

## 4. Authentication, permissions, and rate limits

The authorization flow is:

```text
Bearer token
  -> JWT validation
  -> persisted session validation
  -> user eligibility validation
  -> permission lookup in the database
  -> Customer operation
```

### 4.1 Permission catalog

| Permission | Grants access to |
|---|---|
| `CUSTOMERS_CREATE` | Create Customer |
| `CUSTOMERS_READ` | Get by ID, get by code, and search/list |
| `CUSTOMERS_UPDATE` | Update Customer profile/document |
| `CUSTOMERS_STATUS_CHANGE` | Suspend, reactivate, or close Customer |
| `CUSTOMERS_AUDIT_READ` | Read Customer change audit |
| `CUSTOMERS_PII_READ` | Reserved; no endpoint currently uses this permission |

Possessing one permission does not imply any other permission.

### 4.2 Rate limits

| Endpoint class | Current limit | Partition key |
|---|---:|---|
| Read | 120 requests/minute | Authenticated user subject, otherwise client IP |
| Write | 30 requests/minute | Authenticated user subject, otherwise client IP |

The limits use a fixed one-minute window and do not queue requests. On `429`,
back off and retry after a delay. A `Retry-After` header is not currently
guaranteed.

## 5. Shared data types and rules

### 5.1 Enum values

Enums are currently serialized as JSON numbers.

`CitizenDocumentType`:

| Value | Name | Rule |
|---:|---|---|
| `1` | `VietnamCitizenId` | Exactly 12 ASCII digits; issuing country must be `VN` |
| `2` | `Passport` | 6–20 ASCII letters/digits; any valid ISO alpha-2 issuing country |

`CustomerStatus`:

| Value | Name | Meaning |
|---:|---|---|
| `1` | `Active` | Customer is active |
| `2` | `Suspended` | Customer is temporarily suspended |
| `3` | `Closed` | Terminal state; profile and status can no longer be changed |

For `PATCH /status`, `targetStatus` is a case-insensitive string such as
`"Suspended"`, not a numeric enum.

### 5.2 Citizen document normalization and uniqueness

- Issuing country uses a two-letter ISO alpha-2 code and is normalized to
  uppercase.
- Spaces and hyphens in a submitted document number are removed.
- Letters are normalized to uppercase.
- Uniqueness is evaluated by document type, issuing country, and normalized
  document number.
- `0012-3456-7890` and `001234567890` are therefore the same Vietnam Citizen ID.
- `ab 123456` and `AB123456` are the same passport within the same issuing
  country.
- The raw normalized document number is never returned by these APIs.

### 5.3 Customer detail DTO

```json
{
  "id": 101,
  "customerCode": "CUS_7D4C92A18F20",
  "displayName": "N***",
  "citizenDocumentType": 1,
  "citizenIssuingCountryCode": "VN",
  "citizenIdMasked": "********7890",
  "status": 1,
  "openedOnUtc": "2026-08-11T03:15:20.123456Z",
  "version": 0,
  "createdOnUtc": "2026-08-11T03:15:20.123456Z",
  "updatedOnUtc": "2026-08-11T03:15:20.123456Z"
}
```

Notes:

- `customerCode` is generated by the server, immutable, and currently starts
  with `CUS_`.
- `displayName` is masked. It is not the Customer's full name.
- `citizenIdMasked` exposes only the final four characters.
- `dateOfBirth` and the raw document number are deliberately absent.
- `version` is the optimistic-concurrency token required by update and status
  change requests.

### 5.4 Customer list item DTO

```json
{
  "id": 101,
  "customerCode": "CUS_7D4C92A18F20",
  "displayName": "N***",
  "citizenDocumentType": 1,
  "citizenIdMasked": "********7890",
  "status": 1,
  "openedOnUtc": "2026-08-11T03:15:20.123456Z",
  "version": 0
}
```

## 6. Create Customer

```http
POST /api/customers
Authorization: Bearer <access-token>
Content-Type: application/json
```

Required permission: `CUSTOMERS_CREATE`  
Rate-limit class: write

### 6.1 Create a Vietnamese Customer

Request:

```json
{
  "fullName": "Nguyen Van An",
  "dateOfBirth": "1990-01-02",
  "documentType": 1,
  "issuingCountryCode": "VN",
  "citizenDocumentNumber": "001234567890"
}
```

Response: `200 OK`

```json
{
  "code": "SUCCESS",
  "message": "Customer created.",
  "data": {
    "id": 101,
    "customerCode": "CUS_7D4C92A18F20",
    "displayName": "N***",
    "citizenDocumentType": 1,
    "citizenIssuingCountryCode": "VN",
    "citizenIdMasked": "********7890",
    "status": 1,
    "openedOnUtc": "2026-08-11T03:15:20.123456Z",
    "version": 0,
    "createdOnUtc": "2026-08-11T03:15:20.123456Z",
    "updatedOnUtc": "2026-08-11T03:15:20.123456Z"
  },
  "traceId": "4ac8d29ad9a74b81005b6b579cd5ee3f"
}
```

### 6.2 Create a foreign Customer using a passport

```json
{
  "fullName": "Jean Dupont",
  "dateOfBirth": null,
  "documentType": 2,
  "issuingCountryCode": "FR",
  "citizenDocumentNumber": "12 AB-3456"
}
```

The stored normalized passport is `12AB3456`. The response contains only
`"citizenIdMasked": "****3456"`.

### 6.3 Field rules

| Field | Required | Rules |
|---|---|---|
| `fullName` | Yes | Non-blank, no control characters, maximum 200 characters; whitespace is normalized |
| `dateOfBirth` | No | `YYYY-MM-DD` or `null` |
| `documentType` | Yes | `1` for Vietnam Citizen ID or `2` for passport |
| `issuingCountryCode` | Yes | Exactly two ASCII letters; must be `VN` for document type `1` |
| `citizenDocumentNumber` | Yes | Vietnam ID: 12 digits. Passport: 6–20 ASCII letters/digits after removing spaces/hyphens |

Client-supplied `customerCode`, `status`, `version`, timestamps, ciphertext, or
lookup-hash fields are not part of the request contract and must not be sent.
Unknown JSON fields are currently ignored, but consumers must not depend on
that behavior.

### 6.4 Outcomes

| HTTP | Code | Scenario |
|---:|---|---|
| 200 | `SUCCESS` | Customer and its `CUSTOMER_CREATED` audit event were committed |
| 400 | `BAD_REQUEST` | Invalid name, date format, document type/country/number, or another business input validation failure |
| 400 | framework response | Malformed JSON or model-binding failure |
| 401 | framework response | Missing, invalid, or expired access token |
| 401 | `ADMIN_SESSION_INVALID` | JWT is valid but persisted session/user is no longer valid |
| 403 | framework response | User lacks `CUSTOMERS_CREATE` |
| 403 | `ADMIN_USER_LOCKED` | User account is locked |
| 403 | `ADMIN_USER_INACTIVE` | User account is inactive |
| 403 | `ADMIN_PASSWORD_CHANGE_REQUIRED` | User must change password first |
| 409 | `CUSTOMER_DOCUMENT_CONFLICT` | The normalized document identity already belongs to another Customer, including a concurrent-create race |
| 409 | `CUSTOMER_CODE_CONFLICT` | Server could not allocate a unique Customer code; rare and safe to retry with a new request |
| 429 | framework response | Write limit exceeded |
| 500 | `INTERNAL_SERVER_ERROR` | Unexpected server failure; creation and audit are rolled back together |

Create does not currently support an idempotency key. After an ambiguous
network timeout, do not blindly repeat a create request. Reconcile through an
agreed lookup/operational process first; the public read API cannot search by
raw document number.

## 7. Get Customer by ID

```http
GET /api/customers/101
Authorization: Bearer <access-token>
```

Required permission: `CUSTOMERS_READ`  
Rate-limit class: read

Response: `200 OK`, with `data` containing the Customer detail DTO from section
5.3.

| HTTP | Code | Scenario |
|---:|---|---|
| 200 | `SUCCESS` | Customer found |
| 401 | framework response or `ADMIN_SESSION_INVALID` | Authentication/session failure |
| 403 | framework response or Admin user-state code | Permission/user eligibility failure |
| 404 | `CUSTOMER_NOT_FOUND` | No Customer exists with that ID |
| 429 | framework response | Read limit exceeded |
| 500 | `INTERNAL_SERVER_ERROR` | Unexpected server failure |

An invalid route value such as `/api/customers/abc` does not match this route
and normally returns a framework-generated `404`, not `CUSTOMER_NOT_FOUND`.

## 8. Get Customer by code

```http
GET /api/customers/by-code/CUS_7D4C92A18F20
Authorization: Bearer <access-token>
```

Required permission: `CUSTOMERS_READ`  
Rate-limit class: read

Customer code lookup trims surrounding whitespace and is case-insensitive by
normalizing the code to uppercase.

Response: `200 OK`, with `data` containing the Customer detail DTO.

| HTTP | Code | Scenario |
|---:|---|---|
| 200 | `SUCCESS` | Customer found |
| 401 | framework response or `ADMIN_SESSION_INVALID` | Authentication/session failure |
| 403 | framework response or Admin user-state code | Permission/user eligibility failure |
| 404 | `CUSTOMER_NOT_FOUND` | Customer code does not exist |
| 429 | framework response | Read limit exceeded |
| 500 | `INTERNAL_SERVER_ERROR` | Unexpected server failure |

## 9. Search and paginate Customers

```http
GET /api/customers?status=1&openedFrom=2026-01-01T00:00:00Z&openedTo=2026-12-31T23:59:59Z&page=1&pageSize=20&sortBy=openedOnUtc&sortDirection=desc
Authorization: Bearer <access-token>
```

Required permission: `CUSTOMERS_READ`  
Rate-limit class: read

### 9.1 Query parameters

| Parameter | Required | Default | Behavior |
|---|---|---|---|
| `customerCode` | No | none | Exact normalized Customer code match |
| `status` | No | none | `1`, `2`, or `3`; filters by status |
| `openedFrom` | No | none | Inclusive UTC lower bound; must include `Z`/UTC semantics |
| `openedTo` | No | none | Inclusive UTC upper bound; must include `Z`/UTC semantics |
| `page` | No | `1` | Minimum 1; maximum 1,000,000 |
| `pageSize` | No | `50` | Minimum 1; maximum 100 |
| `sortBy` | No | `openedOnUtc` | Supported: `id`, `customerCode`, `openedOnUtc`, `status` |
| `sortDirection` | No | `desc` | Use `asc` or `desc` |

If `page` or `pageSize` is zero/negative, the transport defaults it to 1 or 50.
Values over the handler limits are clamped. An unsupported `sortBy` falls back
to `openedOnUtc`. Consumers should nevertheless send only documented values.

`openedFrom` later than `openedTo`, or a non-UTC date filter, returns
`400 BAD_REQUEST`.

### 9.2 Response

Response headers:

```http
X-Total-Count: 137
X-Page: 1
X-Page-Size: 20
```

Response body: `200 OK`

```json
{
  "code": "SUCCESS",
  "message": "Success",
  "data": [
    {
      "id": 101,
      "customerCode": "CUS_7D4C92A18F20",
      "displayName": "N***",
      "citizenDocumentType": 1,
      "citizenIdMasked": "********7890",
      "status": 1,
      "openedOnUtc": "2026-08-11T03:15:20.123456Z",
      "version": 0
    }
  ],
  "traceId": "4ac8d29ad9a74b81005b6b579cd5ee3f"
}
```

Pagination metadata is in response headers, not inside `data`.

| HTTP | Code | Scenario |
|---:|---|---|
| 200 | `SUCCESS` | Query completed; `data` may be an empty array |
| 400 | `BAD_REQUEST` | Invalid UTC date range or `openedFrom > openedTo` |
| 400 | framework response | Query parameter cannot be bound, for example an unknown `status` value |
| 401 | framework response or `ADMIN_SESSION_INVALID` | Authentication/session failure |
| 403 | framework response or Admin user-state code | Permission/user eligibility failure |
| 429 | framework response | Read limit exceeded |
| 500 | `INTERNAL_SERVER_ERROR` | Unexpected server failure |

## 10. Update Customer profile

```http
PUT /api/customers/101
Authorization: Bearer <access-token>
Content-Type: application/json
```

Required permission: `CUSTOMERS_UPDATE`  
Rate-limit class: write

This operation uses optimistic concurrency. First read the Customer and use its
latest `version` as `expectedVersion`.

### 10.1 Update without replacing the document

All three nullable document fields must be `null` to keep the current document:

```json
{
  "expectedVersion": 0,
  "fullName": "Nguyen Van An Updated",
  "dateOfBirth": "1990-01-02",
  "documentType": null,
  "issuingCountryCode": null,
  "citizenDocumentNumber": null
}
```

### 10.2 Update and replace the document

If any document field is supplied, all three fields are required:

```json
{
  "expectedVersion": 0,
  "fullName": "Jean Dupont",
  "dateOfBirth": null,
  "documentType": 2,
  "issuingCountryCode": "FR",
  "citizenDocumentNumber": "12AB3456"
}
```

Response: `200 OK`, with the updated Customer detail DTO. A successful profile
update increments `version` by one and atomically writes a
`CUSTOMER_PROFILE_UPDATED` audit event.

### 10.3 Outcomes

| HTTP | Code | Scenario |
|---:|---|---|
| 200 | `SUCCESS` | Profile and audit event committed; version incremented |
| 400 | `BAD_REQUEST` | Invalid name/document, or only part of a replacement document was supplied |
| 400 | framework response | Malformed JSON/model-binding failure |
| 401 | framework response or `ADMIN_SESSION_INVALID` | Authentication/session failure |
| 403 | framework response or Admin user-state code | Missing `CUSTOMERS_UPDATE` or user not eligible |
| 404 | `CUSTOMER_NOT_FOUND` | Customer ID does not exist |
| 409 | `CUSTOMER_CONCURRENCY_CONFLICT` | `expectedVersion` does not equal the current version when loaded |
| 409 | `CONCURRENCY_CONFLICT` | Database detected a concurrent write during commit |
| 409 | `CUSTOMER_DOCUMENT_CONFLICT` | Replacement document belongs to another Customer |
| 409 | `CUSTOMER_CLOSED` | Closed Customer profile cannot be updated |
| 429 | framework response | Write limit exceeded |
| 500 | `INTERNAL_SERVER_ERROR` | Unexpected server failure; mutation and audit are rolled back |

On either concurrency code, perform a fresh GET and reconcile changes. Do not
automatically overwrite the newer state. Retry with the new version only after
the caller decides that the intended change is still valid.

## 11. Change Customer status

```http
PATCH /api/customers/101/status
Authorization: Bearer <access-token>
Content-Type: application/json
```

Required permission: `CUSTOMERS_STATUS_CHANGE`  
Rate-limit class: write

Request:

```json
{
  "expectedVersion": 1,
  "targetStatus": "Suspended",
  "reason": "Manual compliance review"
}
```

`targetStatus` is case-insensitive. Supported names are `Active`, `Suspended`,
and `Closed`. `reason` is required, is trimmed, and has a maximum length of 500
characters.

### 11.1 Allowed state transitions

```text
Active -----> Suspended
  |               |
  |               +-----> Closed
  +---------------------> Closed

Suspended -----> Active

Closed: terminal; no outgoing transitions
```

Changing to the current status is invalid. A successful transition increments
`version` by one and atomically creates a `CUSTOMER_STATUS_CHANGED` audit event.

Response: `200 OK`, with the updated Customer detail DTO.

### 11.2 Outcomes

| HTTP | Code | Scenario |
|---:|---|---|
| 200 | `SUCCESS` | Status and audit event committed; version incremented |
| 400 | `BAD_REQUEST` | Unsupported target status, blank reason, or reason longer than 500 characters |
| 400 | framework response | Malformed JSON/model-binding failure |
| 401 | framework response or `ADMIN_SESSION_INVALID` | Authentication/session failure |
| 403 | framework response or Admin user-state code | Missing `CUSTOMERS_STATUS_CHANGE` or user not eligible |
| 404 | `CUSTOMER_NOT_FOUND` | Customer ID does not exist |
| 409 | `CUSTOMER_CONCURRENCY_CONFLICT` | Stale `expectedVersion` detected when loaded |
| 409 | `CONCURRENCY_CONFLICT` | Database detected a concurrent write during commit |
| 409 | `CUSTOMER_INVALID_STATUS_TRANSITION` | Transition is disallowed, including any transition from `Closed` |
| 429 | framework response | Write limit exceeded |
| 500 | `INTERNAL_SERVER_ERROR` | Unexpected server failure; mutation and audit are rolled back |

## 12. Read Customer audit

```http
GET /api/customers/101/audit?skip=0&take=20
Authorization: Bearer <access-token>
```

Required permission: `CUSTOMERS_AUDIT_READ`  
Rate-limit class: read

| Parameter | Required | Default | Rule |
|---|---|---|---|
| `skip` | No | `0` | Clamped to 0–1,000,000 |
| `take` | No | `50` | Clamped to 1–100; zero/negative defaults to 50 |

Events are ordered newest first.

Response: `200 OK`

```json
{
  "code": "SUCCESS",
  "message": "Success",
  "data": [
    {
      "eventId": 9002,
      "eventType": "CUSTOMER_STATUS_CHANGED",
      "changedFieldsJson": "{\"Status\":{\"Before\":\"Active\",\"After\":\"Suspended\"}}",
      "fromStatus": "Active",
      "toStatus": "Suspended",
      "reason": "Manual compliance review",
      "outcome": "SUCCESS",
      "actorUserId": "12",
      "traceId": "4ac8d29ad9a74b81005b6b579cd5ee3f",
      "occurredOnUtc": "2026-08-11T04:10:00.123456Z"
    }
  ],
  "traceId": "9edc69dc50db4202b8a1d6537a8df48d"
}
```

`changedFieldsJson` is currently a JSON-encoded string rather than a nested JSON
object. If a consumer needs its fields, parse that string as JSON in a second
step. Its shape varies by `eventType` and should be treated as audit detail, not
as the authoritative current Customer state.

Current event types:

| Event type | Created by |
|---|---|
| `CUSTOMER_CREATED` | Successful create |
| `CUSTOMER_PROFILE_UPDATED` | Successful profile/document update |
| `CUSTOMER_STATUS_CHANGED` | Successful status transition |

Names, dates of birth, and document identities inside audit changes are masked.
The audit response does not include the raw Citizen ID/passport number.

| HTTP | Code | Scenario |
|---:|---|---|
| 200 | `SUCCESS` | Audit read completed; `data` may be an empty array |
| 400 | framework response | Query parameter cannot be bound |
| 401 | framework response or `ADMIN_SESSION_INVALID` | Authentication/session failure |
| 403 | framework response or Admin user-state code | Missing `CUSTOMERS_AUDIT_READ` or user not eligible |
| 404 | `CUSTOMER_NOT_FOUND` | Customer ID does not exist |
| 429 | framework response | Read limit exceeded |
| 500 | `INTERNAL_SERVER_ERROR` | Unexpected server failure |

## 13. Error-code catalog

### 13.1 Customer business codes

| HTTP | Code | Meaning | Consumer action |
|---:|---|---|---|
| 404 | `CUSTOMER_NOT_FOUND` | Customer does not exist | Stop or refresh the caller's reference |
| 409 | `CUSTOMER_CODE_CONFLICT` | Server-generated code could not be made unique | Retry later; escalate if repeated |
| 409 | `CUSTOMER_DOCUMENT_CONFLICT` | Normalized Citizen ID/passport is already assigned | Do not retry unchanged input; investigate duplicate identity |
| 409 | `CUSTOMER_INVALID_STATUS_TRANSITION` | Requested lifecycle transition is not allowed | Refresh Customer and follow the state machine |
| 409 | `CUSTOMER_CONCURRENCY_CONFLICT` | Expected version is stale | Reload, reconcile, then retry intentionally |
| 409 | `CUSTOMER_CLOSED` | Closed Customer cannot be updated | Stop; `Closed` is terminal |

### 13.2 Common application codes

| HTTP | Code | Meaning | Consumer action |
|---:|---|---|---|
| 200 | `SUCCESS` | Operation succeeded | Consume `data` |
| 400 | `BAD_REQUEST` | Business input validation failed | Correct request; do not retry unchanged input |
| 409 | `CONCURRENCY_CONFLICT` | Database write race detected | Reload and reconcile |
| 500 | `INTERNAL_SERVER_ERROR` | Unexpected server error | Record `traceId`; use bounded retry only where operation semantics are safe |

### 13.3 Authentication/user-state codes visible on Customer calls

| HTTP | Code | Meaning | Consumer action |
|---:|---|---|---|
| 401 | `ADMIN_SESSION_INVALID` | Persisted login session is invalid/revoked/expired | Re-authenticate; do not keep retrying the token |
| 403 | `ADMIN_USER_LOCKED` | User is locked | Stop and contact an administrator |
| 403 | `ADMIN_USER_INACTIVE` | User is inactive | Stop and contact an administrator |
| 403 | `ADMIN_PASSWORD_CHANGE_REQUIRED` | User must change password | Complete the password-change flow |

`ADMIN_PERMISSION_DENIED` is recorded in the security audit when permission
authorization fails, but the current framework-generated `403` response does
not guarantee that code in its response body.

## 14. End-to-end integration scenarios

### Scenario A: create and retain the server identity

1. Authenticate through the Auth API and obtain an access token.
2. Call `POST /api/customers` with `CUSTOMERS_CREATE`.
3. Require HTTP `200` and `code == SUCCESS`.
4. Persist the returned `id`, `customerCode`, and `version` in the integrating
   system if required.
5. Do not expect full name, date of birth, or raw document number in responses.

### Scenario B: handle a duplicate CCCD/passport

1. Submit the create or document-replacement request.
2. Receive `409 CUSTOMER_DOCUMENT_CONFLICT`.
3. Do not retry the same identity in a loop.
4. Route the case to the agreed duplicate/reconciliation process. The API does
   not disclose which existing Customer owns that document.

### Scenario C: safe optimistic-concurrency update

1. GET the Customer and store `version = N`.
2. PUT the profile with `expectedVersion = N`.
3. On success, replace the local version with returned `N + 1`.
4. On `CUSTOMER_CONCURRENCY_CONFLICT` or `CONCURRENCY_CONFLICT`, GET again.
5. Compare the newer state with the intended change; retry only after a safe
   merge decision.

### Scenario D: suspend and reactivate

1. GET the current Customer.
2. If status is `Active`, PATCH to `Suspended` with the current version and a
   meaningful reason.
3. Later, GET again and PATCH `Suspended -> Active` using the latest version.
4. Do not reuse a version returned before the prior transition.

### Scenario E: close a Customer

1. Confirm the business decision outside this API because close is terminal.
2. GET the latest Customer and version.
3. PATCH to `Closed` with a reason.
4. Treat `200 SUCCESS` as terminal. Subsequent update/status calls will fail.

### Scenario F: paginate a full export safely

1. Request a bounded `pageSize` no greater than 100.
2. Read `X-Total-Count`, `X-Page`, and `X-Page-Size`.
3. Continue page by page using a stable documented sort.
4. Be aware this is offset pagination over live data; concurrent inserts or
   updates can change later pages. It is not a snapshot-export contract.
5. Respect `429` and back off.

### Scenario G: investigate a mutation

1. Use an operator with `CUSTOMERS_AUDIT_READ`.
2. GET `/api/customers/{id}/audit`.
3. Correlate the event's `traceId`, `actorUserId`, event type, and occurrence
   timestamp.
4. Treat masked change fields as evidence of change, not a source of raw PII.

### Scenario H: process protected-request failures

```text
401 -> obtain a new authenticated session; do not retry the same invalid token
403 -> stop and request the required permission/user remediation
404 CUSTOMER_NOT_FOUND -> stop using the stale Customer reference
409 document conflict -> duplicate-identity workflow
409 concurrency -> reload and reconcile
409 closed/invalid transition -> follow lifecycle rules; do not blind retry
429 -> exponential backoff with jitter
500 -> record traceId and retry only when operation semantics make it safe
```

## 15. cURL examples

Set these placeholders in the calling environment:

```text
BASE_URL=https://api.example.com
ACCESS_TOKEN=<access-token>
```

Create:

```bash
curl -X POST "$BASE_URL/api/customers" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-Id: partner-create-0001" \
  -d '{
    "fullName": "Nguyen Van An",
    "dateOfBirth": "1990-01-02",
    "documentType": 1,
    "issuingCountryCode": "VN",
    "citizenDocumentNumber": "001234567890"
  }'
```

Search:

```bash
curl "$BASE_URL/api/customers?status=1&page=1&pageSize=20&sortBy=openedOnUtc&sortDirection=desc" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Accept: application/json"
```

Update while retaining the document:

```bash
curl -X PUT "$BASE_URL/api/customers/101" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "expectedVersion": 0,
    "fullName": "Nguyen Van An Updated",
    "dateOfBirth": "1990-01-02",
    "documentType": null,
    "issuingCountryCode": null,
    "citizenDocumentNumber": null
  }'
```

Suspend:

```bash
curl -X PATCH "$BASE_URL/api/customers/101/status" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "expectedVersion": 1,
    "targetStatus": "Suspended",
    "reason": "Manual compliance review"
  }'
```

Read audit:

```bash
curl "$BASE_URL/api/customers/101/audit?skip=0&take=20" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Accept: application/json"
```

## 16. Consumer implementation checklist

- Use HTTPS and protect bearer tokens and request PII.
- Check HTTP status before parsing the Friday response envelope.
- Branch on `code`, never localized `message`.
- Preserve and report `traceId`/correlation identifiers on failures.
- Treat enum response values as the documented numeric contract for this API
  version.
- Never send or expect client-controlled Customer code, status, version, or
  system timestamps during create.
- Always use the latest returned `version` for mutation requests.
- Handle both Customer-specific and common concurrency codes.
- Never blind-retry create after an ambiguous timeout; idempotency is not yet
  implemented.
- Never blind-retry a document conflict or invalid state transition.
- Respect `429` with bounded exponential backoff and jitter.
- Do not assume paginated results form a point-in-time snapshot.
- Do not expect raw full name, date of birth, CCCD, or passport data from the
  current Customer read APIs.

## 17. Current contract limitations

The following are explicit properties of the current implementation and may be
addressed by a future versioned contract:

- Create returns `200 OK`, not `201 Created`.
- Create has no idempotency key.
- Authentication challenge, permission denial, rate limiting, and some
  model-binding failures do not guarantee the Friday error envelope.
- Enums are numeric in normal JSON DTOs, while status mutation uses a string.
- Pagination uses offsets and is not a snapshot/export cursor.
- `changedFieldsJson` is a JSON string rather than a nested object.
- `CUSTOMERS_PII_READ` is reserved; there is no raw-PII read endpoint.
- There is no public lookup by raw Citizen ID/passport number.
