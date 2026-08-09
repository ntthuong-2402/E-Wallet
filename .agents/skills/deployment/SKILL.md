---
name: deployment
description: Use for Docker, local compose, CI/CD, security scans, observability, migrations, deployment, rollback, runbooks, and release-to-QA/staging practices.
---


# Deployment Workflow

- Build Linux multi-stage runtime images; use non-root users when possible.
- Never bake secrets into images.
- Separate liveness/readiness.
- Use immutable image tags/digests for controlled environments.
- Run migrations as a controlled deployment step; avoid uncontrolled production auto-migration.
- Pipeline baseline: restore/format/build/test/security scans/image build/image scan/publish/deploy/smoke.
- Keep database migrations backward compatible when old/new app versions can overlap.
- Use OpenTelemetry and dashboards for API latency/errors, payment status, queue depth/DLQ, DB health, cache, provider behavior, and webhook delivery.
- Document rollback/forward-fix and provider/queue outage runbooks.

Read `references/RELEASE_CHECKLIST.md` before a release/QA handoff task.

