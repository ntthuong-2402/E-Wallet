# Release / QA Checklist

- Build passes with pinned dependency lock as configured.
- Critical unit/integration/contract/architecture tests pass.
- Migration upgrade verified; dangerous schema changes reviewed.
- No unapproved critical/high security findings.
- Secret/dependency/container scans completed.
- Immutable image tag/digest recorded.
- Environment secrets not present in repo/image/logs.
- Health readiness/liveness works.
- Smoke: login/health/create-query transaction or payment path as appropriate.
- RabbitMQ consumer path verified when changed.
- OpenAPI/event change notes updated.
- Rollback/forward-fix and DB migration notes prepared.
- `.codex/state/VERIFICATION.md` reflects actual evidence, not assumptions.
