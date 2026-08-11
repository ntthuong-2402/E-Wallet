# Admin / Identity Review — 2026-08-10

## Scope and conclusion

Admin currently owns authentication, JWT issuance, users, roles, rights, sessions, bootstrap, and security audit. It is a strong Identity/Admin module for the modular-monolith stage, but not yet the independently deployable `Identity.Api` from the target topology.

Overall readiness against the intended Admin/Identity architecture: **79%**.

| Dimension | Score | Current evidence |
|---|---:|---|
| Core identity/admin capability | 92% | Bootstrap, password lifecycle, users, roles/rights, assignment/removal, sessions, audit. |
| Authorization/security controls | 84% | Named rights, DB permission checks, lockout, session validation, last-admin guard, external secrets. |
| Persistence/reliability | 88% | PostgreSQL mappings, FKs, unique refresh hash, reuse detection, optimistic refresh concurrency. |
| Automated verification | 78% | 4 domain unit + 7 API integration tests; PostgreSQL migration smoke verified manually. |
| Target deployable architecture | 55% | Clean module layers/schema, but hosted in `Friday.API` with shared `FridayDbContext`. |

## Confirmed current capabilities

- First-start system administrator provisioning from environment with mandatory first password change.
- JWT access tokens plus hashed refresh tokens, rotation, token-family reuse detection, revocation, and concurrent-refresh rejection.
- Active, locked, temporary-lockout, persisted-session, and mandatory-password-change checks.
- Per-endpoint permission policies backed by active role/right database lookup.
- User create/read/update, reset password, lock/unlock, activate/deactivate, bounded search/filter, session list/revoke.
- Role/right create/list, role-right replacement, role assignment and removal.
- Last-`SUPER_ADMIN` protection, serialized on PostgreSQL with a role-row lock.
- Durable security audit with actor/target/time/event filters.
- Internal FKs for role joins and unique refresh-token hashes.
- Secrets excluded from tracked config and Docker build context.

## Verification evidence

- Solution build: 0 errors.
- Unit tests: 4 passed.
- API integration tests: 7 passed.
- Disposable PostgreSQL smoke: schema migrated, both P1 FKs present, refresh-token hash index unique.
- Docker Compose valid; `.env` ignored; configured local secrets absent from tracked source.

## Remaining gaps before production banking use

### High priority

1. MFA for `SUPER_ADMIN` and privileged operators.
2. Production signing-key lifecycle: asymmetric signing/JWKS or external IdP, key ID, rotation, emergency revocation.
3. Automated PostgreSQL fixture for migrations, constraints, unique refresh hash, and concurrent refresh; current relational proof is manual smoke.
4. Recovery/invitation process for privileged accounts; public registration stays disabled in controlled environments.
5. Audit retention/archival and explicit append-only operational policy.

### Medium priority

1. Update/activate/deactivate role lifecycle and explicit policy for custom dynamic rights.
2. Revoke one selected device/session rather than only all sessions.
3. Options validation with `ValidateOnStart` for JWT/bootstrap/password policy.
4. Replace direct `DateTime.UtcNow` with `TimeProvider` in expiry and lockout code.
5. Add invalid-signature/issuer/audience/expiry, rate-limit, and migration rollback/forward-fix tests.

## Architecture fit

The current `Domain <- Application <- Infrastructure` layering, thin endpoints, Admin schema, and coarse-grained capability boundary align with the original direction. Keeping it as a documented module is appropriate for the current team size.

The target topology remains incomplete because authentication runs inside `Friday.API`, Admin shares `FridayDbContext` and physical migrations, and no independent `Identity.Api` exists. Extraction should wait for deployment lifecycle, scale, ownership, or isolation needs; first isolate contracts and persistence ownership without cross-service SQL.

## Next recommended milestone

Treat MFA, production key management, and automated PostgreSQL security tests as P2 release blockers. Treat `Identity.Api` extraction as an architecture milestone, not a prerequisite for the next internal QA build.
