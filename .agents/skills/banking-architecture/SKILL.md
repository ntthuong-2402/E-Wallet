---
name: banking-architecture
description: Use for service decomposition, bounded contexts, data ownership, ADRs, API/event boundaries, and architecture reviews. Do not use for detailed ledger math or auth implementation unless architecture ownership is the primary question.
---


# Banking Architecture Workflow

1. Identify the business capability and owning bounded context.
2. Confirm whether the change belongs in an existing coarse-grained deployable unit before proposing a new service.
3. Map data ownership. No cross-service SQL or foreign keys.
4. Identify synchronous API vs asynchronous event boundaries and failure modes.
5. Check operational cost: deployment, observability, testing, versioning, retries, eventual consistency.
6. Prefer a documented module boundary over a new microservice until independent scaling/lifecycle/ownership justifies extraction.
7. Record durable decisions in `.codex/state/DECISIONS.md` and promote significant ones to an ADR.

Read `references/ARCHITECTURE_BASELINE.md` only when the task needs the project-wide baseline.

