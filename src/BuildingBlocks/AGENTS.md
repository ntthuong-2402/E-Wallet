# BuildingBlocks Instructions

Apply these rules with the repository-root `AGENTS.md`.

## Scope

BuildingBlocks is shared by multiple modules. Read `.codex/state/MODULE_PATTERN.md` and `.codex/state/PERSISTENCE_PATTERN.md` before changing CQRS behavior, persistence, migrations, caching, localization, or domain primitives.

## Boundary rules

- Add code here only when it is genuinely cross-cutting and has more than one credible module consumer.
- Do not add new module-specific error codes, business events, entities, DTOs, or policies to BuildingBlocks.
- Domain contains shared domain primitives only; Application contains shared ports/behaviors; Infrastructure contains technical implementations.
- Preserve inward project dependencies. BuildingBlocks Domain must not reference Application or Infrastructure.
- Do not introduce a generic repository merely to share CRUD operations.

## Shared persistence cautions

- `FridayDbContext`, `EfUnitOfWork`, migrations, and configuration discovery affect every installed module.
- Preserve the normal command order unless an explicit task changes it: begin transaction, handler, `SaveChangesAsync`, in-process domain-event dispatch, relational commit.
- Do not add handler-level commits as a general pattern. The Admin registration flow is a known exception, not a template.
- A model change requires checking EF configuration, migration/snapshot impact, provider behavior, rollback/forward-fix, and all modules sharing the context.
- Domain/integration notifications are currently in-process and non-durable; do not describe them as broker delivery or an outbox.

## Verification

- Build all directly affected BuildingBlocks and consuming module projects.
- For persistence changes, verify transaction failure behavior and migration compatibility. Clearly report that automated persistence tests are absent unless the task adds them.
