# Friday Admin API Integration Guide

Version: 1.0  
Updated: 2026-08-11  
Scope: current Auth and Admin endpoints implemented by `Friday.API`

## 1. Purpose

This document is the consumer-facing contract for authentication and Admin
operations. It describes current runtime behavior, including routes, request and
response bodies, permissions, business scenarios, HTTP statuses, and
application error codes.

Example base URL:

```text
https://api.example.com
```

All JSON property names use `camelCase`. All timestamps are UTC ISO 8601 values.

## 2. Common request headers

```http
Content-Type: application/json
Accept: application/json
Accept-Language: vi-VN
X-Correlation-Id: partner-request-000123
Authorization: Bearer <access-token>
```

- `Authorization` is required for `/api/admin/*` and
  `/api/auth/change-password`.
- `Accept-Language` is optional. Error messages may be localized; consumers
  must branch on `code`, never on `message`.
- `X-Correlation-Id` is optional and is echoed in the response header. Use
  `traceId` from the body when reporting an application error.
- Never log or transmit access tokens, refresh tokens, or passwords outside a
  protected TLS channel.

## 3. Common response envelope

Successful application responses use HTTP 200:

```json
{
  "code": "SUCCESS",
  "message": "Success",
  "data": {},
  "traceId": "4ac8d29ad9a74b81005b6b579cd5ee3f"
}
```

Application failures normally use:

```json
{
  "code": "ADMIN_USER_NOT_FOUND",
  "message": "User '999' was not found.",
  "data": null,
  "traceId": "4ac8d29ad9a74b81005b6b579cd5ee3f"
}
```

Important exception: authentication challenge and permission-forbidden
responses generated directly by ASP.NET Core are not currently guaranteed to
use this envelope. Consumers must always check the HTTP status first.

## 4. Authentication and authorization model

```text
Login
  -> access token + refresh token + user
  -> Bearer access token on protected request
  -> JWT validation
  -> persisted session validation
  -> active/unlocked/password-change eligibility
  -> endpoint permission check
  -> endpoint operation
```

Access tokens contain the user ID and session ID. A cryptographically valid JWT
is not sufficient by itself: the persisted session must still be active and
the user must remain active and unlocked.

Admin endpoints require both authentication and the permission shown for that
endpoint.

### Permission catalog

| Permission | Purpose |
|---|---|
| `USERS_READ` | Read users and their sessions |
| `USERS_CREATE` | Create users |
| `USERS_UPDATE` | Update user profile/identity fields |
| `USERS_LOCK` | Lock users |
| `USERS_UNLOCK` | Unlock users |
| `USERS_ACTIVATE` | Activate users |
| `USERS_DEACTIVATE` | Deactivate users |
| `USERS_RESET_PASSWORD` | Set a temporary password |
| `USERS_REVOKE_SESSIONS` | Revoke all sessions for a user |
| `USERS_ASSIGN_ROLE` | Assign a role to a user |
| `USERS_REMOVE_ROLE` | Remove a role from a user |
| `ROLES_READ` | Read roles |
| `ROLES_MANAGE` | Create roles and replace role rights |
| `RIGHTS_READ` | Read rights |
| `RIGHTS_MANAGE` | Create rights |
| `AUDIT_READ` | Read security audit events |

Permission failure currently produces HTTP 403. The system records
`ADMIN_PERMISSION_DENIED` in security audit, but that value is not yet a stable
response-body code because framework-generated forbidden responses are not
customized.

## 5. Shared DTOs

### Quick cURL example

```bash
curl -X POST "https://api.example.com/api/auth/login" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-Id: partner-login-0001" \
  -d '{"login":"system-admin","password":"<password>"}'
```

```bash
curl "https://api.example.com/api/admin/users?skip=0&take=20" \
  -H "Authorization: Bearer <access-token>" \
  -H "Accept-Language: vi-VN" \
  -H "X-Correlation-Id: partner-users-0001"
```

### User

```json
{
  "id": 12,
  "userCode": "OPS_0012",
  "username": "operator.12",
  "email": "operator.12@example.com",
  "fullName": "Nguyen Van Operator",
  "phone": "+84901234567",
  "address": "Ho Chi Minh City",
  "companyName": "Example Company",
  "jobTitle": "Operations Specialist",
  "notes": "Night shift",
  "isActive": true,
  "isLocked": false,
  "mustChangePassword": false,
  "roleIds": [2, 5]
}
```

Nullable fields: `phone`, `address`, `companyName`, `jobTitle`, `notes`.

### Role

```json
{
  "id": 2,
  "code": "OPERATIONS",
  "name": "Operations",
  "isActive": true,
  "rightIds": [1, 2, 7]
}
```

### Right

```json
{
  "id": 7,
  "code": "USERS_READ",
  "name": "Read users",
  "description": "Allows reading Admin users"
}
```

### User session

```json
{
  "id": "554d06ee-310c-4d34-a127-e45f961d7474",
  "createdOnUtc": "2026-08-11T02:30:00Z",
  "expiresAtUtc": "2026-08-25T02:30:00Z",
  "revokedAtUtc": null,
  "ipAddress": "203.0.113.10",
  "userAgent": "PartnerPortal/1.0"
}
```

## 6. Auth APIs

Auth endpoints are rate limited by the `auth-strict` policy: currently 10
requests per one-minute window, with no queue. Rate-limit rejection returns HTTP
429; its response body is framework-generated and is not a stable application
envelope.

### 6.1 Register

```http
POST /api/auth/register
```

Authentication: public when enabled by environment configuration.  
Configuration: `Authentication:AllowPublicRegistration`.

Request:

```json
{
  "username": "external.operator",
  "email": "external.operator@example.com",
  "password": "Str0ngExamplePassword!",
  "fullName": "External Operator",
  "phone": "+84901234567"
}
```

The system generates `userCode`. A successful registration also logs the user
in and returns the same shape as Login.

Success: HTTP 200 / `SUCCESS`.

Common failures:

| HTTP | Code | Scenario |
|---:|---|---|
| 403 | `ADMIN_REGISTRATION_DISABLED` | Public registration is disabled |
| 400 | `ADMIN_PASSWORD_POLICY_VIOLATION` | Password fails configured policy |
| 400 | `ADMIN_USER_USERNAME_EXISTS` | Username already exists |
| 400 | `ADMIN_USER_EMAIL_EXISTS` | Email already exists |
| 429 | framework response | Rate limit exceeded |

### 6.2 Login

```http
POST /api/auth/login
```

`login` accepts username, email, or user code.

Request:

```json
{
  "login": "system-admin",
  "password": "<password>"
}
```

Success:

```json
{
  "code": "SUCCESS",
  "message": "Success",
  "data": {
    "accessToken": "<jwt-access-token>",
    "accessTokenExpiresAtUtc": "2026-08-11T03:30:00Z",
    "refreshToken": "<opaque-refresh-token>",
    "user": {
      "id": 1,
      "userCode": "SYSTEM_ADMIN",
      "username": "system-admin",
      "email": "admin@example.com",
      "fullName": "System Administrator",
      "phone": null,
      "address": null,
      "companyName": null,
      "jobTitle": null,
      "notes": null,
      "isActive": true,
      "isLocked": false,
      "mustChangePassword": true,
      "roleIds": [1]
    }
  },
  "traceId": "4ac8d29ad9a74b81005b6b579cd5ee3f"
}
```

Common failures:

| HTTP | Code | Scenario |
|---:|---|---|
| 401 | `ADMIN_INVALID_CREDENTIALS` | Unknown login or incorrect password |
| 429 | `ADMIN_ACCOUNT_TEMPORARILY_LOCKED` | Failed-attempt lockout is active |
| 403 | `ADMIN_USER_INACTIVE` | Credentials are correct but user is inactive |
| 403 | `ADMIN_USER_LOCKED` | Credentials are correct but user is administratively locked |
| 429 | framework response | Endpoint rate limit exceeded |

Default security configuration is five failed attempts followed by a 15-minute
temporary lockout. Configuration may differ by environment.

### 6.3 First-login password change

When `user.mustChangePassword` is `true`, the access token is valid only for:

- `POST /api/auth/change-password`;
- `POST /api/auth/logout`.

Other authenticated resources return:

| HTTP | Code |
|---:|---|
| 403 | `ADMIN_PASSWORD_CHANGE_REQUIRED` |

Change request:

```http
POST /api/auth/change-password
Authorization: Bearer <access-token>
```

```json
{
  "currentPassword": "<current-password>",
  "newPassword": "<new-strong-password>"
}
```

Success:

```json
{
  "code": "SUCCESS",
  "message": "Success",
  "data": true,
  "traceId": "4ac8d29ad9a74b81005b6b579cd5ee3f"
}
```

All existing sessions are revoked after success. The client must log in again
using the new password.

Failures:

| HTTP | Code | Scenario |
|---:|---|---|
| 401 | authentication/framework response | Missing or invalid access token |
| 401 | `ADMIN_SESSION_INVALID` | Token does not identify a valid session/user |
| 404 | `ADMIN_USER_NOT_FOUND` | Authenticated user no longer exists |
| 401 | `ADMIN_CURRENT_PASSWORD_INVALID` | Current password is incorrect |
| 400 | `ADMIN_PASSWORD_POLICY_VIOLATION` | New password fails policy or equals current password |
| 429 | framework response | Rate limit exceeded |

Current default password policy: 12-128 characters, at least one uppercase
letter, one lowercase letter, and one digit. Environment configuration is
authoritative.

### 6.4 Refresh token

```http
POST /api/auth/refresh
```

Request:

```json
{
  "refreshToken": "<opaque-refresh-token>"
}
```

Success:

```json
{
  "code": "SUCCESS",
  "message": "Success",
  "data": {
    "accessToken": "<new-jwt-access-token>",
    "accessTokenExpiresAtUtc": "2026-08-11T04:30:00Z",
    "refreshToken": "<new-opaque-refresh-token>"
  },
  "traceId": "4ac8d29ad9a74b81005b6b579cd5ee3f"
}
```

The refresh token is rotated. Replace the old refresh token atomically after a
successful response. Reusing a consumed token can revoke its token family.

Failures:

| HTTP | Code | Scenario |
|---:|---|---|
| 401 | `ADMIN_INVALID_REFRESH_TOKEN` | Invalid, expired, revoked, reused, or concurrently consumed token |
| 401 | `ADMIN_SESSION_INVALID` | User/session is no longer eligible |
| 429 | framework response | Rate limit exceeded |

### 6.5 Logout

```http
POST /api/auth/logout
```

Request:

```json
{
  "refreshToken": "<opaque-refresh-token>"
}
```

Success is intentionally idempotent. An unknown/already revoked token also
returns HTTP 200:

```json
{
  "code": "SUCCESS",
  "message": "Success",
  "data": true,
  "traceId": "4ac8d29ad9a74b81005b6b579cd5ee3f"
}
```

## 7. User administration APIs

All endpoints below require `Authorization: Bearer <access-token>`.

### 7.1 Create user

```http
POST /api/admin/users
Permission: USERS_CREATE
```

```json
{
  "userCode": "OPS_0012",
  "username": "operator.12",
  "email": "operator.12@example.com",
  "fullName": "Nguyen Van Operator",
  "password": "TemporaryPassw0rd!",
  "phone": "+84901234567",
  "address": "Ho Chi Minh City",
  "companyName": "Example Company",
  "jobTitle": "Operations Specialist",
  "notes": "Night shift",
  "roleIds": [2, 5]
}
```

Success: HTTP 200 / `SUCCESS` with a User DTO.

| HTTP | Code | Scenario |
|---:|---|---|
| 400 | `ADMIN_PASSWORD_POLICY_VIOLATION` | Password fails policy |
| 400 | `ADMIN_USER_USERNAME_EXISTS` | Duplicate username |
| 400 | `ADMIN_USER_EMAIL_EXISTS` | Duplicate email |
| 400 | `ADMIN_USER_CODE_EXISTS` | Duplicate user code |
| 400 | `ADMIN_USER_ROLE_NOT_FOUND` | At least one role ID does not exist |

### 7.2 Search/list users

```http
GET /api/admin/users?skip=0&take=50&search=operator&isActive=true&isLocked=false&roleId=2
Permission: USERS_READ
```

Parameters:

| Parameter | Type | Default/behavior |
|---|---|---|
| `skip` | integer | Default 0; negative values become 0 |
| `take` | integer | Default 50; clamped to 1-200 |
| `search` | string | Searches supported user identity/profile fields |
| `isActive` | boolean | Optional active filter |
| `isLocked` | boolean | Optional lock filter |
| `roleId` | integer | Optional assigned-role filter |

Pagination metadata is returned in headers:

```http
X-Total-Count: 143
X-Skip: 0
X-Take: 50
```

`data` is a JSON array of User DTOs, not a page wrapper.

### 7.3 Get user by ID

```http
GET /api/admin/users/12
Permission: USERS_READ
```

Success: HTTP 200 with a User DTO.  
Not found: HTTP 404 / `ADMIN_USER_NOT_FOUND`.

### 7.4 Update user

```http
PUT /api/admin/users/12
Permission: USERS_UPDATE
```

```json
{
  "userCode": "OPS_0012",
  "username": "operator.12",
  "email": "operator.12@example.com",
  "fullName": "Nguyen Van Operator Updated",
  "phone": "+84907654321",
  "address": "Ha Noi",
  "companyName": "Example Company",
  "jobTitle": "Senior Operations Specialist",
  "notes": "Updated by HR"
}
```

This is a complete profile update contract; omitted non-nullable properties may
fail binding or domain validation. It cannot change password, roles, active
state, or lock state.

| HTTP | Code | Scenario |
|---:|---|---|
| 404 | `ADMIN_USER_NOT_FOUND` | User does not exist |
| 400 | `ADMIN_USER_CODE_EXISTS` | User code belongs to another user |
| 400 | `ADMIN_USER_USERNAME_EXISTS` | Username belongs to another user |
| 400 | `ADMIN_USER_EMAIL_EXISTS` | Email belongs to another user |

### 7.5 Reset user password

```http
POST /api/admin/users/12/password
Permission: USERS_RESET_PASSWORD
```

```json
{
  "newPassword": "NewTemporaryPassw0rd!"
}
```

Success returns the User DTO with `mustChangePassword: true`. All user sessions
are revoked. The user must log in with the temporary password and then call
Change Password.

| HTTP | Code | Scenario |
|---:|---|---|
| 400 | `ADMIN_PASSWORD_POLICY_VIOLATION` | Password fails policy |
| 404 | `ADMIN_USER_NOT_FOUND` | User does not exist |

### 7.6 Assign role

```http
POST /api/admin/users/12/roles/2
Permission: USERS_ASSIGN_ROLE
```

No request body. Success returns the updated User DTO.

| HTTP | Code | Scenario |
|---:|---|---|
| 404 | `ADMIN_USER_NOT_FOUND` | User does not exist |
| 404 | `ADMIN_ROLE_NOT_FOUND` | Role does not exist or is inactive |

Assigning an already assigned role is treated as an idempotent aggregate
operation and returns the current User DTO.

### 7.7 Remove role

```http
DELETE /api/admin/users/12/roles/2
Permission: USERS_REMOVE_ROLE
```

No request body. Success returns the updated User DTO and revokes all sessions
for that user.

| HTTP | Code | Scenario |
|---:|---|---|
| 404 | `ADMIN_USER_NOT_FOUND` | User does not exist |
| 404 | `ADMIN_ROLE_NOT_FOUND` | Role does not exist |
| 409 | `ADMIN_LAST_ADMINISTRATOR_PROTECTION` | Would remove `SUPER_ADMIN` from the last assigned administrator |

### 7.8 Lock user

```http
POST /api/admin/users/12/lock
Permission: USERS_LOCK
```

Success returns the updated User DTO and revokes all sessions.

| HTTP | Code | Scenario |
|---:|---|---|
| 404 | `ADMIN_USER_NOT_FOUND` | User does not exist |
| 409 | `ADMIN_LAST_ADMINISTRATOR_PROTECTION` | Would disable the last enabled super administrator |

### 7.9 Unlock user

```http
POST /api/admin/users/12/unlock
Permission: USERS_UNLOCK
```

Success returns the updated User DTO.  
Not found: HTTP 404 / `ADMIN_USER_NOT_FOUND`.

### 7.10 Activate user

```http
POST /api/admin/users/12/activate
Permission: USERS_ACTIVATE
```

Success returns the updated User DTO.  
Not found: HTTP 404 / `ADMIN_USER_NOT_FOUND`.

### 7.11 Deactivate user

```http
POST /api/admin/users/12/deactivate
Permission: USERS_DEACTIVATE
```

Success returns the updated User DTO and revokes all sessions.

| HTTP | Code | Scenario |
|---:|---|---|
| 404 | `ADMIN_USER_NOT_FOUND` | User does not exist |
| 409 | `ADMIN_LAST_ADMINISTRATOR_PROTECTION` | Would disable the last enabled super administrator |

### 7.12 List user sessions

```http
GET /api/admin/users/12/sessions
Permission: USERS_READ
```

Success returns an array of User session DTOs. The current implementation
returns an empty array when no sessions exist; it does not separately validate
that the user ID exists.

### 7.13 Revoke all user sessions

```http
POST /api/admin/users/12/sessions/revoke
Permission: USERS_REVOKE_SESSIONS
```

Success returns `data: true`. The current implementation is idempotent and does
not separately return not-found for an unknown user ID.

## 8. Role APIs

### 8.1 Create role

```http
POST /api/admin/roles
Permission: ROLES_MANAGE
```

```json
{
  "code": "OPERATIONS",
  "name": "Operations"
}
```

Success: HTTP 200 with a Role DTO.  
Duplicate code: HTTP 400 / `ADMIN_ROLE_CODE_EXISTS`.

### 8.2 List roles

```http
GET /api/admin/roles
Permission: ROLES_READ
```

Success returns an array of Role DTOs including each role's `rightIds`.

### 8.3 Replace rights assigned to a role

```http
POST /api/admin/roles/2/rights
Permission: ROLES_MANAGE
```

The request body is a raw JSON integer array:

```json
[1, 2, 7]
```

This replaces the complete right set; it is not an incremental add operation.
Send `[]` to remove all rights.

| HTTP | Code | Scenario |
|---:|---|---|
| 404 | `ADMIN_ROLE_NOT_FOUND` | Role does not exist |
| 400 | `ADMIN_RIGHT_NOT_FOUND` | At least one right ID does not exist |

## 9. Right APIs

### 9.1 Create right

```http
POST /api/admin/rights
Permission: RIGHTS_MANAGE
```

```json
{
  "code": "REPORTS_READ",
  "name": "Read reports",
  "description": "Allows reading operational reports"
}
```

Success: HTTP 200 with a Right DTO.  
Duplicate code: HTTP 400 / `ADMIN_RIGHT_CODE_EXISTS`.

Creating a right only adds it to the catalog. It has no effect until assigned
to a role.

### 9.2 List rights

```http
GET /api/admin/rights
Permission: RIGHTS_READ
```

Success returns an array of Right DTOs.

## 10. Security audit API

```http
GET /api/admin/audit-events?skip=0&take=50&eventType=LOGIN_FAILED&actorUserId=12&targetType=USER&targetId=12&fromUtc=2026-08-01T00:00:00Z&toUtc=2026-09-01T00:00:00Z
Permission: AUDIT_READ
```

Parameters:

| Parameter | Type | Default/behavior |
|---|---|---|
| `skip` | integer | Default 0; negative becomes 0 |
| `take` | integer | Default 50; clamped to 1-200 |
| `eventType` | string | Optional exact event type filter |
| `actorUserId` | integer | Optional actor filter |
| `targetType` | string | Optional target type filter |
| `targetId` | string | Optional target identifier filter |
| `fromUtc` | UTC timestamp | Optional inclusive lower boundary |
| `toUtc` | UTC timestamp | Optional inclusive upper boundary |

Success returns:

```json
{
  "code": "SUCCESS",
  "message": "Success",
  "data": [
    {
      "id": 501,
      "eventType": "LOGIN_FAILED",
      "actorUserId": 12,
      "targetType": "USER",
      "targetId": "12",
      "outcome": "FAILURE",
      "reasonCode": "ADMIN_INVALID_CREDENTIALS",
      "ipAddress": "203.0.113.10",
      "traceId": "4ac8d29ad9a74b81005b6b579cd5ee3f",
      "occurredOnUtc": "2026-08-11T02:30:00Z"
    }
  ],
  "traceId": "03e0cdb5773b476389381dd0823e6269"
}
```

The endpoint does not currently return total-count pagination metadata.

Common event types include:

```text
LOGIN_SUCCEEDED
LOGIN_FAILED
LOGIN_LOCKED_OUT
REFRESH_TOKEN_ROTATED
REFRESH_TOKEN_REJECTED
REFRESH_TOKEN_REUSE_DETECTED
LOGOUT_SUCCEEDED
PASSWORD_CHANGED
PASSWORD_RESET
USER_REGISTERED
USER_CREATED
USER_UPDATED
USER_LOCKED
USER_UNLOCKED
USER_ACTIVATED
USER_DEACTIVATED
ROLE_ASSIGNED
ROLE_REMOVED
SESSIONS_REVOKED
ROLE_CREATED
ROLE_RIGHTS_CHANGED
RIGHT_CREATED
PERMISSION_DENIED
```

## 11. Error-code catalog

### Common codes

| HTTP | Code | Meaning |
|---:|---|---|
| 200 | `SUCCESS` | Operation succeeded |
| 400 | `BAD_REQUEST` | Invalid argument/domain input not mapped to a more specific code |
| 404 | `NOT_FOUND` | Generic resource-not-found mapping |
| 409 | `CONCURRENCY_CONFLICT` | Persisted state changed concurrently |
| 500 | `INTERNAL_SERVER_ERROR` | Unexpected server failure |

### Admin codes

| Typical HTTP | Code | Meaning |
|---:|---|---|
| 404 | `ADMIN_USER_NOT_FOUND` | User does not exist |
| 400 | `ADMIN_USER_USERNAME_EXISTS` | Username already exists |
| 400 | `ADMIN_USER_EMAIL_EXISTS` | Email already exists |
| 400 | `ADMIN_USER_CODE_EXISTS` | User code already exists |
| 400 | `ADMIN_USER_ROLE_NOT_FOUND` | One or more role IDs do not exist |
| 401 | `ADMIN_INVALID_CREDENTIALS` | Login identifier/password is invalid |
| 403 | `ADMIN_USER_INACTIVE` | User is inactive |
| 403 | `ADMIN_USER_LOCKED` | User is administratively locked |
| 429 | `ADMIN_ACCOUNT_TEMPORARILY_LOCKED` | Failed-login lockout is active |
| 400 | `ADMIN_PASSWORD_REQUIRED` | Password is required where applicable |
| 400 | `ADMIN_PASSWORD_POLICY_VIOLATION` | Password violates configured policy |
| 401 | `ADMIN_CURRENT_PASSWORD_INVALID` | Current password is incorrect |
| 403 | `ADMIN_PASSWORD_CHANGE_REQUIRED` | First-login/temporary password must be changed |
| 403/audit | `ADMIN_PERMISSION_DENIED` | Permission rejected; stable response code not yet guaranteed |
| 409 | `ADMIN_LAST_ADMINISTRATOR_PROTECTION` | Action would remove/disable the last super admin |
| 401 | `ADMIN_INVALID_REFRESH_TOKEN` | Refresh token is invalid, expired, reused, or consumed |
| 401 | `ADMIN_SESSION_INVALID` | Session/user eligibility failed |
| 403 | `ADMIN_REGISTRATION_DISABLED` | Public registration is disabled |
| 404 | `ADMIN_ROLE_NOT_FOUND` | Role does not exist/inactive where required |
| 400 | `ADMIN_ROLE_CODE_EXISTS` | Role code already exists |
| 400 | `ADMIN_RIGHT_NOT_FOUND` | One or more rights do not exist |
| 400 | `ADMIN_RIGHT_CODE_EXISTS` | Right code already exists |

The table lists typical current mappings. Consumers must use the received HTTP
status and `code`; do not infer retryability from the message text.

## 12. Integration scenarios

### Scenario A: bootstrap administrator first login

```text
1. POST /api/auth/login with provisioned credentials.
2. Receive tokens and user.mustChangePassword = true.
3. POST /api/auth/change-password using the access token.
4. Existing session is revoked.
5. POST /api/auth/login using the new password.
6. Use the new access/refresh token pair.
```

### Scenario B: normal token rotation

```text
1. Login and store the refresh token securely.
2. Access token approaches expiry.
3. POST /api/auth/refresh once.
4. Atomically replace both access and refresh tokens.
5. Never reuse the previous refresh token.
```

Only one concurrent refresh should be attempted per session. When two refresh
requests race, one can succeed and the other returns
`ADMIN_INVALID_REFRESH_TOKEN`.

### Scenario C: create an operator and grant access

```text
1. GET /api/admin/roles and /api/admin/rights.
2. Create or select a role.
3. POST /api/admin/roles/{roleId}/rights with the complete right set.
4. POST /api/admin/users with roleIds and a temporary password.
5. If needed, POST /api/admin/users/{userId}/password later.
6. User logs in and changes the temporary password when required.
```

### Scenario D: immediately remove access

Use one of:

```text
POST /api/admin/users/{userId}/lock
POST /api/admin/users/{userId}/deactivate
POST /api/admin/users/{userId}/sessions/revoke
DELETE /api/admin/users/{userId}/roles/{roleId}
```

Lock, deactivate, password reset, role removal, and explicit session revocation
revoke existing sessions as described in their endpoint sections.

### Scenario E: handle a failed protected request

```text
401 -> refresh only when the failure is compatible with token/session expiry;
       otherwise require login.
403 ADMIN_PASSWORD_CHANGE_REQUIRED -> navigate to change-password flow.
403 without stable code -> treat as insufficient permission or framework denial.
409 -> reload current state before deciding whether to retry.
429 -> back off; do not retry in a tight loop.
500 -> report traceId and correlation ID to the API operator.
```

## 13. Consumer implementation checklist

- Use HTTPS only.
- Store refresh tokens in secure server-side or platform-protected storage.
- Do not place tokens/passwords in URLs, logs, analytics, or exception messages.
- Serialize refresh requests per session.
- Replace the refresh token only after HTTP 200.
- Branch on HTTP status and application `code`, not localized `message`.
- Capture `traceId` and `X-Correlation-Id` for support cases.
- Respect 429 responses and back off.
- Treat role-right replacement as a full replacement operation.
- Read user-list pagination metadata from response headers.
- Do not assume every framework 401/403 currently has the common envelope.
