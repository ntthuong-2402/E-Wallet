---
name: identity-security
description: Use for authentication, OAuth/OIDC, JWT validation, refresh rotation, RBAC/permissions, MFA, partner credentials, HMAC/mTLS, secrets, PII, rate limiting, and security reviews.
---


# Identity and Security Workflow

1. Identify actor: end user, admin/operator, partner, internal service.
2. Separate authentication, authorization, and business eligibility.
3. For JWTs, validate signature/issuer/audience/expiry locally using trusted keys/JWKS where appropriate; do not add per-request Identity calls without a protocol reason.
4. For partner signing, define canonical bytes and replay window before coding.
5. Apply least privilege and resource ownership checks.
6. Treat credentials/secrets as non-loggable and non-committable.
7. Audit security-relevant actions and failures without leaking sensitive data.
8. Test unauthorized, forbidden, replay, invalid signature, expired token, over-posting, and rate-limit paths where applicable.

Read `references/SECURITY_CHECKLIST.md` when doing a security-impacting change.

