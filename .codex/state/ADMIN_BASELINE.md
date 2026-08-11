# Friday Admin Authentication and Authorization Baseline

> Superseded 2026-08-10 for current behavior. See `.codex/state/ADMIN_REVIEW.md`. Permissions, lockout, durable audit, refresh reuse/concurrency protection, role removal/last-admin protection, bounded lists, internal FKs, and unique refresh hashes now exist. The remaining text is retained as historical pre-P0/P1 evidence.

Scope: existing Login, JWT, User, Role, Right/Permission, refresh-session, and protected-endpoint behavior only. Findings were verified against executable source while excluding `bin/`, `obj/`, and unrelated modules.

Status vocabulary: **CONFIRMED**, **PARTIAL**, **NOT_IMPLEMENTED**, **UNKNOWN**.

## 1. Executive Baseline

- Authentication: **CONFIRMED** JWT bearer authentication with symmetric HMAC-SHA256 signing.
- Login: **CONFIRMED** database-backed user/password verification and session creation.
- Refresh token: **CONFIRMED** opaque token generation, hashed persistence, rotation, expiry, and revocation.
- User eligibility: **CONFIRMED** active/locked checks at login, refresh, and on each authenticated request.
- Role model: **CONFIRMED** user-role persistence and active role-code claims.
- Permission model: **PARTIAL**. `Right` entities and role-right assignments exist, but rights are not loaded into tokens or enforced.
- Authorization: **PARTIAL**. Admin endpoints require an authenticated principal only; role/permission authorization is **NOT_IMPLEMENTED**.

The current actors are ordinary authenticated users and users operating Admin endpoints. The implementation has no technical distinction that proves an authenticated caller is an administrator.

## 2. Login Flow

```text
POST /api/auth/login
  -> LoginCommand
  -> LoginCommandHandler.HandleAsync
  -> UserRepository.GetByLoginWithPasswordAsync
  -> PasswordHasher.VerifyHashedPassword
  -> User active/locked checks
  -> RoleRepository.GetByIdsAsync
  -> refresh token + UserSession creation
  -> JwtTokenIssuer.CreateAccessToken
  -> TransactionBehavior / EfUnitOfWork commit
  -> ApiResponse<LoginResponseDto>
```

### Step 1: HTTP endpoint

- File: `src/API/Friday.API/Modules/Auth/AuthEndpoints.cs`
- Class: `AuthEndpoints`
- Method: `MapAuthModule` (`POST /api/auth/login` delegate)
- Responsibility: bind JSON to `LoginCommand`, pass it to `IMediator.SendAsync`, wrap the result with `ApiResults.Ok`.
- Input: `LoginCommand(Login, Password)`.
- Output: HTTP 200 containing `ApiResponse<LoginResponseDto>` on success.
- Authorization metadata: none; login is public.
- Error behavior: exceptions propagate to `ExceptionHandlingMiddleware`.

### Step 2: command

- File: `src/Modules/Admin/Friday.Modules.Admin.Application/Features/Auth/Login.cs`
- Class: `LoginCommand`
- Method: record construction/model binding
- Responsibility: CQRS request contract carrying login identifier and plaintext password from the request boundary to the handler.

### Step 3: handler and transaction boundary

- File: `src/Modules/Admin/Friday.Modules.Admin.Application/Features/Auth/Login.cs`
- Class: `LoginCommandHandler`
- Method: `HandleAsync`
- Responsibility: orchestrate lookup, credential verification, eligibility, roles, session creation, JWT issuance, and response construction.
- File: `src/BuildingBlocks/Friday.BuildingBlocks.Application/Behaviors/TransactionBehavior.cs`
- Class: `TransactionBehavior<TRequest,TResponse>`
- Method: `HandleAsync`
- Responsibility: wrap every command, including login, in begin/commit/rollback operations.
- File: `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Persistence/EfUnitOfWork.cs`
- Class: `EfUnitOfWork`
- Methods: `BeginTransactionAsync`, `CommitAsync`, `RollbackAsync`
- Responsibility: open relational transaction, save EF changes, dispatch tracked domain events, then commit; rollback on failure.

### Step 4: user lookup

- File: `src/Modules/Admin/Friday.Modules.Admin.Infrastructure/Repositories/UserRepository.cs`
- Class: `UserRepository`
- Method: `GetByLoginWithPasswordAsync`
- Responsibility: normalize the supplied login and query a user by case-insensitive username, normalized email, or normalized user code.
- EF graph: eagerly loads `UserRoles` and `PasswordCredential`.
- Database objects: `admin.users`, `admin.user_roles`, `admin.user_passwords`.
- Result: tracked `User?`; no `AsNoTracking` is used.

### Step 5: password verification

- File: `src/Modules/Admin/Friday.Modules.Admin.Infrastructure/DependencyInjection.cs`
- Class: `DependencyInjection`
- Method: `AddAdminInfrastructure`
- Responsibility: register `PasswordHasher<CredentialUser>` as `IPasswordHasher<CredentialUser>`.
- File: `src/Modules/Admin/Friday.Modules.Admin.Application/Features/Auth/Login.cs`
- Class: `LoginCommandHandler`
- Method: `HandleAsync`
- Responsibility: invoke `VerifyHashedPassword` against `UserPassword.PasswordHash`.
- Failure: missing user/password row and failed hash verification both produce `ADMIN_INVALID_CREDENTIALS`, HTTP 401, with the same public message.
- Detail: `SuccessRehashNeeded` is accepted as successful but does not trigger rehash persistence.
- Failed-attempt counter/automatic lockout: **NOT_IMPLEMENTED**.

### Step 6: user eligibility

- File: `src/Modules/Admin/Friday.Modules.Admin.Application/Features/Auth/Login.cs`
- Class: `LoginCommandHandler`
- Method: `HandleAsync`
- Responsibility: reject `!IsActive` with HTTP 403 and reject `IsLocked` with HTTP 403.
- File: `src/Modules/Admin/Friday.Modules.Admin.Domain/Aggregates/UserAggregate/User.cs`
- Class: `User`
- Methods: `Lock`, `Unlock`
- Responsibility: maintain explicit administrative lock state.
- Business eligibility is separate from password authentication, but it is evaluated only after successful password verification.

### Step 7: role and permission loading

- File: `src/Modules/Admin/Friday.Modules.Admin.Application/Features/Auth/Login.cs`
- Class: `LoginCommandHandler`
- Method: `HandleAsync`
- Responsibility: extract role IDs from `User.UserRoles`, query role entities, retain distinct codes for active roles.
- File: `src/Modules/Admin/Friday.Modules.Admin.Infrastructure/Repositories/RoleRepository.cs`
- Class: `RoleRepository`
- Method: `GetByIdsAsync`
- Responsibility: query matching `Role` rows.
- Role output: active role codes only.
- `RoleRights` loading in this method: **NOT_IMPLEMENTED**.
- Permission/right loading during login: **NOT_IMPLEMENTED**.

### Step 8: refresh session creation

- File: `src/Modules/Admin/Friday.Modules.Admin.Application/Security/RefreshTokenUtilities.cs`
- Class: `RefreshTokenUtilities`
- Methods: `GenerateOpaqueToken`, `Hash`
- Responsibility: generate 32 cryptographically random bytes as Base64 and SHA-256 hash the token to lowercase hexadecimal.
- File: `src/Modules/Admin/Friday.Modules.Admin.Domain/Aggregates/UserAggregate/UserSession.cs`
- Class: `UserSession`
- Method: `Create`
- Responsibility: create a new session ID, store user ID, refresh hash, UTC expiry/creation time, IP address, and user agent.
- File: `src/Modules/Admin/Friday.Modules.Admin.Infrastructure/Repositories/UserSessionRepository.cs`
- Class: `UserSessionRepository`
- Method: `AddAsync`
- Responsibility: attach the new session to the shared EF context; persistence occurs at command commit.

### Step 9: JWT generation

- File: `src/Modules/Admin/Friday.Modules.Admin.Application/Security/IJwtTokenIssuer.cs`
- Interface: `IJwtTokenIssuer`
- Method: `CreateAccessToken`
- Responsibility: application port accepting user ID, session ID, and role codes.
- File: `src/Modules/Admin/Friday.Modules.Admin.Infrastructure/Security/JwtTokenIssuer.cs`
- Class: `JwtTokenIssuer`
- Method: `CreateAccessToken`
- Responsibility: construct, sign, serialize, and return the access token and UTC expiry.

### Step 10: response

- File: `src/Modules/Admin/Friday.Modules.Admin.Application/Models/AuthDtos.cs`
- Class: `LoginResponseDto`
- Responsibility: return `AccessToken`, `AccessTokenExpiresAtUtc`, raw `RefreshToken`, and `UserDto`.
- File: `src/API/Friday.API/Common/ApiResults.cs`
- Class: `ApiResults`
- Method: `Ok`
- Responsibility: wrap the DTO in an API response containing code, message, data, and trace ID.
- Error response: `ExceptionHandlingMiddleware.InvokeAsync` maps `FridayException` into localized JSON failure envelopes.

## 3. JWT Flow

### Configuration and registration

- File: `src/Modules/Admin/Friday.Modules.Admin.Application/Configuration/JwtSettings.cs`
- Class: `JwtSettings`
- Section: `Authentication:Jwt`
- Defaults: issuer `Friday.API`, audience `Friday.Clients`, access token 60 minutes, refresh token 14 days.
- File: `src/API/Friday.API/Program.cs`
- Method: top-level authentication registration
- Responsibility: require a secret of at least 32 characters and register the JWT bearer default scheme.
- Validation flags: issuer, audience, lifetime, issuer signing key are all enabled.
- Key: UTF-8 bytes of the configured symmetric secret.
- Explicit `ClockSkew`: **NOT_IMPLEMENTED**; framework default behavior applies.
- Authentication event hooks/challenge customization: **NOT_IMPLEMENTED**.
- Explicit inbound claim-map configuration: **NOT_IMPLEMENTED**.

### Issuance

- Signing algorithm: HMAC-SHA256.
- `iss`: configured issuer.
- `aud`: configured audience.
- `nbf`: current UTC time.
- `exp`: current UTC time plus clamped access lifetime (1 minute through 24 hours).
- Claims:
  - `sub`: integer user ID as string.
  - `jti`: persisted `UserSession.Id` GUID as string.
  - `iat`: Unix timestamp as string.
  - one `ClaimTypes.Role` claim per distinct active role code.
- Permission/right claims: **NOT_IMPLEMENTED**.
- Key ID, rotation, asymmetric keys, JWKS: **NOT_IMPLEMENTED**.

### Request authentication and session validation

- File: `src/API/Friday.API/Program.cs`
- Method: middleware pipeline
- Responsibility: execute `UseAuthentication`, then `UseAuthorization`, then `AuthenticatedUserValidationMiddleware`.
- File: `src/API/Friday.API/Middlewares/AuthenticatedUserValidationMiddleware.cs`
- Class: `AuthenticatedUserValidationMiddleware`
- Method: `InvokeAsync`
- Responsibility: for an authenticated principal, resolve user ID from name-identifier/`sub`, resolve session GUID from `jti`, load the session and user, and reject invalid/revoked/expired/mismatched sessions or inactive/locked users.
- Important ordering: endpoint authorization runs before database session/user validation. The validation middleware still stops the endpoint afterward when session/user state is invalid.
- Cost: every authenticated request performs session and user database queries.

## 4. Refresh Token Flow

Overall status: **CONFIRMED**.

### Login issuance

- Raw token returned once to the client.
- SHA-256 hash stored in `admin.user_sessions`.
- Refresh duration is clamped to 1–365 days.

### Refresh

- File: `src/API/Friday.API/Modules/Auth/AuthEndpoints.cs`
- Class/method: `AuthEndpoints.MapAuthModule`, `POST /api/auth/refresh`
- File: `src/Modules/Admin/Friday.Modules.Admin.Application/Features/Auth/RefreshToken.cs`
- Class/method: `RefreshTokenCommandHandler.HandleAsync`
- Responsibility: hash input, find an unrevoked/unexpired session, reload user and active role codes, rotate to a new refresh hash/expiry on the same session, and issue a new JWT.
- Transaction: command transaction persists rotation at commit.
- Previous refresh token: invalid after successful commit because its hash is replaced.

### Logout and administrative revocation

- File: `src/Modules/Admin/Friday.Modules.Admin.Application/Features/Auth/Logout.cs`
- Class/method: `LogoutCommandHandler.HandleAsync`
- Responsibility: hash the supplied token, revoke the matching session if open, and always return true.
- File: `src/Modules/Admin/Friday.Modules.Admin.Application/Features/Users/LockUser.cs`
- Class/method: `LockUserHandler.HandleAsync`
- Responsibility: lock user and revoke all open sessions in the same command transaction.

### Refresh limitations

- Concurrent refresh protection: **NOT_IMPLEMENTED**; `UserSession` has no concurrency token/row version.
- Token-family/reuse detection: **NOT_IMPLEMENTED**.
- Refresh hash unique constraint: **NOT_IMPLEMENTED**; it has a non-unique index.
- Rotation history/audit record: **NOT_IMPLEMENTED**.
- Device/session management endpoints: **NOT_IMPLEMENTED**.
- Password-reset session revocation: **NOT_IMPLEMENTED**.
- Refresh/logout endpoint rate limiting: **NOT_IMPLEMENTED**.

## 5. User Model

- File: `src/Modules/Admin/Friday.Modules.Admin.Domain/Aggregates/UserAggregate/User.cs`
- Class: `User`
- Responsibility: aggregate root for identity/profile fields, active/locked eligibility, password credential navigation, and assigned role IDs.
- Identifiers accepted at login: username, email, or user code.
- Normalization: user code uppercase, email lowercase; username is stored trimmed but queried case-insensitively using `ToUpper()`.
- Status: `IsActive` and `IsLocked`.
- Password: one-to-one `UserPassword` row containing ASP.NET Identity hash.
- Roles: `UserRoles` collection of join records.
- Locking revokes sessions through `LockUserHandler`, not through the domain `Lock()` method itself.
- Password reset changes the hash without revoking sessions.

## 6. Role and Permission Model

The code calls permissions **Rights**.

### Role storage and behavior

- File: `src/Modules/Admin/Friday.Modules.Admin.Domain/Aggregates/RoleAggregate/Role.cs`
- Class: `Role`
- Methods: `Create`, `SetRights`
- Responsibility: uppercase role code, display name, active flag, and role-right collection.
- Table: `admin.roles`; unique index on `Code`.
- User assignment table: `admin.user_roles`, composite key `(UserId, RoleId)`.
- Login/refresh use only active role codes.
- Role removal/deactivation operations: **NOT_IMPLEMENTED** in inspected code.

### Right storage and behavior

- File: `src/Modules/Admin/Friday.Modules.Admin.Domain/Aggregates/RightAggregate/Right.cs`
- Class: `Right`
- Method: `Create`
- Responsibility: uppercase right code, name, description.
- Table: `admin.rights`; unique index on `Code`.
- Assignment table: `admin.role_rights`, composite key `(RoleId, RightId)`.
- File: `src/Modules/Admin/Friday.Modules.Admin.Application/Features/Roles/GrantRightsToRole.cs`
- Class: `GrantRightsToRoleHandler`
- Method: `HandleAsync`
- Responsibility: validate role and right IDs, then replace the role’s complete right set.

### Database relationship limits

- `admin.user_roles.UserId` -> `admin.users.Id`: **CONFIRMED FK**, cascade delete.
- `admin.user_roles.RoleId` -> `admin.roles.Id`: **NOT_IMPLEMENTED FK** in current EF configuration/migration.
- `admin.role_rights.RoleId` -> `admin.roles.Id`: **CONFIRMED FK**, cascade delete.
- `admin.role_rights.RightId` -> `admin.rights.Id`: **NOT_IMPLEMENTED FK** in current EF configuration/migration.
- Application handlers validate role/right existence for normal writes, but the database does not enforce both referenced sides.

### Token and authorization use

- Role codes in JWT: **CONFIRMED**.
- Right IDs/codes in JWT: **NOT_IMPLEMENTED**.
- Rights loaded during login/refresh: **NOT_IMPLEMENTED**.
- Database right lookup during protected requests: **NOT_IMPLEMENTED**.
- Role or right changes revoke sessions: **NOT_IMPLEMENTED**.
- Existing JWT role claims can remain stale until token replacement or expiry.

## 7. Authorization Flow

Representative protected request: `GET /api/admin/users`.

```text
Bearer request
  -> UseAuthentication: cryptographic JWT validation and ClaimsPrincipal
  -> UseAuthorization: default authenticated-user policy
  -> AuthenticatedUserValidationMiddleware: session/user database eligibility
  -> Admin endpoint delegate
  -> mediator/query handler
```

- File: `src/API/Friday.API/Modules/Admin/AdminEndpoints.cs`
- Class: `AdminEndpoints`
- Method: `MapAdminModule`
- Responsibility: map `/api/admin` and apply group-level `.RequireAuthorization()`.
- File: `src/API/Friday.API/Program.cs`
- Method: `AddAuthorization` / middleware pipeline
- Responsibility: register default authorization services and execute authorization middleware.

Confirmed behavior:

- Any authenticated principal satisfying the default policy reaches the later session/user validation step.
- Any valid, unrevoked session for an active/unlocked user can reach every Admin endpoint.
- This includes user creation/update/listing, password reset, user locking, role assignment, role/right creation, and right assignment.
- Role claims are present but never required.
- Rights are stored but never used for authorization.

Not implemented:

- named authorization policies;
- custom authorization handlers;
- role requirements;
- permission requirements;
- endpoint permission metadata;
- resource/ownership checks;
- database permission resolution;
- fallback deny-by-default policy;
- Admin actor distinction;
- endpoint rate limiting.

## 8. Database Objects Involved

Shared context:

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Persistence/FridayDbContext.cs`
- Class: `FridayDbContext`
- Method: `OnModelCreating`
- Responsibility: load Admin `IEntityTypeConfiguration<T>` mappings into the shared model. It has no explicit `DbSet<T>` properties; repositories use `Set<T>()`.

| Object | Entity | Authentication/authorization use |
|---|---|---|
| `admin.users` | `User` | Login identity/profile and active/locked status. |
| `admin.user_passwords` | `UserPassword` | One-to-one password hash; PK/FK `UserId`, cascade delete. |
| `admin.user_sessions` | `UserSession` | Refresh hash, expiry, revocation, IP/user-agent; UUID PK; FK to user. |
| `admin.user_roles` | `UserRole` | User-role IDs; composite PK; FK only to user. |
| `admin.roles` | `Role` | Active role codes and names. |
| `admin.role_rights` | `RoleRight` | Role-right IDs; composite PK; FK only to role. |
| `admin.rights` | `Right` | Permission/right catalog. |

The login command reads users/passwords/user_roles/roles and writes user_sessions. It does not read rights/role_rights.

## 9. Missing or Unclear Parts

### Confirmed missing

1. Role- or permission-based endpoint enforcement.
2. Permission loading into claims or per-request authorization.
3. Failed-login throttling, counters, or automatic lockout.
4. Rate limiting for login, refresh, logout, and Admin endpoints.
5. MFA and security-audit persistence.
6. JWT key rotation, key identifier, asymmetric validation, or JWKS.
7. Refresh concurrency control, reuse detection, and rotation history.
8. Session revocation on password reset or role/right change.
9. Database foreign keys from `user_roles.RoleId` and `role_rights.RightId`.
10. Automated authentication/authorization tests.

### Unclear / requires runtime or business confirmation

1. **UNKNOWN** intended mapping from right codes to endpoints/actions.
2. **UNKNOWN** which role, if any, is intended to represent an administrator.
3. **UNKNOWN** production secret source and key-rotation procedure.
4. **UNKNOWN** desired behavior for role changes against already-issued access tokens.
5. **UNKNOWN** expected concurrent-refresh semantics and whether reuse should revoke a token family.
6. **UNKNOWN** required session retention and security-audit retention.
7. **UNKNOWN** effective runtime claim mapping without runtime inspection; source does not customize it.

## 10. Recommended Next Analysis

1. Define and verify the authorization matrix: endpoint/action -> required role/right -> resource ownership rule.
2. Perform isolated runtime tests for login, expired/invalid JWTs, revoked sessions, inactive/locked users, refresh rotation/replay, and logout.
3. Analyze concurrent refresh requests against PostgreSQL and document the required winner/reuse behavior.
4. Trace password reset, lock/unlock, role assignment, and right changes for required session invalidation and audit events.
5. Verify migration constraints against a disposable PostgreSQL database, especially missing join-table foreign keys.
