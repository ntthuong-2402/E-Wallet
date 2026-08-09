# Security Checklist

- HTTPS/TLS and restricted infrastructure network exposure.
- OAuth2/OIDC flow appropriate to actor; short-lived access tokens.
- Refresh-token rotation/reuse detection strategy.
- MFA for admin/operator where in scope.
- Deny-by-default policy authorization and resource ownership.
- Partner client credentials scoped and rotatable.
- HMAC canonical request spec: method, normalized path/query, body SHA-256, timestamp, nonce, encoding, key id.
- Replay protection and clock-skew window.
- Constant-time signature comparison.
- Secrets outside Git; no secrets/PII/token/private key in logs.
- Rate limits for login and partner endpoints.
- Exact CORS origins; restrict Swagger/admin endpoints outside development.
- Audit login failures, permission denials, credential rotation, security events, financial state transitions.
