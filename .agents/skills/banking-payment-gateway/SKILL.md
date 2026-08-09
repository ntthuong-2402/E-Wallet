---
name: banking-payment-gateway
description: Design, implement, review, or verify a C# Banking Payment Gateway using controlled microservices, payment orchestration, double-entry ledger, idempotency, RabbitMQ outbox/inbox, PostgreSQL, Valkey, Docker, security, testing, and CI/CD. Use for banking/payment architecture tasks, payment or ledger changes, QA readiness, security review, and phased delivery. Do not use for unrelated generic CRUD projects.
---

# Banking Payment Gateway workflow

## Required inputs

Collect from the task and repository:
- requested behavior or review target;
- affected bounded context;
- current solution/service structure;
- acceptance criteria and business rules;
- relevant API, event, schema, migration, and deployment files;
- available test and CI commands.

Do not assume planned files already exist.

## Step 1 — Classify the task

Classify as one or more of:
- architecture/design;
- implementation;
- payment/ledger correctness;
- security;
- debugging;
- test/QA verification;
- performance/resilience;
- documentation/ADR.

Use the matching custom agents when parallel review improves quality. Keep implementation ownership with one bounded worker after reviewers return evidence.

## Step 2 — Load the right references

Read only the references needed:
- `references/architecture-baseline.md` for boundaries and deployment decisions;
- `references/domain-decisions.md` for clarified financial/security semantics;
- `references/coding-and-verification.md` for implementation and test gates;
- `references/source-map.md` for traceability to the original architecture prompt.

## Step 3 — Establish invariants before changes

For every financial write, answer:
1. What is the durable idempotency key and uniqueness scope?
2. What database transaction commits business state and outbox together?
3. How does a duplicate request/message return or converge to the same result?
4. What concurrency mechanism protects the debit account or ledger account?
5. What happens when the provider times out with an unknown outcome?
6. Which state transitions are legal?
7. How is reversal represented without mutating posted journals?
8. What audit, metric, and trace evidence is produced?

For every security-sensitive change, answer:
1. Who authenticates and how?
2. Which scope/permission/resource policy authorizes the action?
3. What input can be attacker-controlled?
4. What secret or PII could reach logs or messages?
5. What rate, replay, and abuse controls apply?
6. What negative test proves denial?

## Step 4 — Plan minimally

Prefer a small coherent change inside an existing deployable unit. Do not create a new service, shared database access, generic repository, global mutable helper, or new infrastructure dependency without evidence that it is required.

When a rule is unresolved, create an ADR/open-decision item rather than embedding an arbitrary rule in code.

## Step 5 — Implement or review

Implementation:
- preserve Clean Architecture dependency direction;
- keep transport thin;
- make transaction boundaries explicit;
- use outbox/inbox for cross-process reliability;
- use typed, versioned contracts;
- configure decimal precision, indexes, constraints, and optimistic/pessimistic concurrency deliberately;
- add structured telemetry without sensitive values.

Review:
- trace actual execution paths;
- lead with evidence-backed defects;
- separate confirmed defects, design risks, and unresolved decisions;
- suggest the smallest safe remediation and missing test.

## Step 6 — Verify

Run the repository's actual checks. Prefer:
- format/build;
- domain unit tests;
- PostgreSQL/RabbitMQ/Valkey integration tests;
- migration tests;
- API/event contract tests;
- duplicate and same-account concurrency tests;
- Docker image build and smoke tests;
- security/secret/dependency/container scans where configured.

Never convert “configured” into “verified.” Record skipped checks and blockers.

## Output format

Return:
1. scope and assumptions;
2. evidence or files inspected;
3. decisions/invariants;
4. changes or findings ordered by severity;
5. tests/commands and exact results;
6. unresolved decisions and risks;
7. next smallest actionable step.
