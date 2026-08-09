# Admin Module Instructions

Apply these rules with the repository-root `AGENTS.md`.

## Scope

Admin owns the current login, JWT issuance, users, roles, rights, and sessions. Use `.codex/state/ADMIN_BASELINE.md` and `.codex/state/FRIDAY_DEVELOPMENT_PATTERN.md` as source indexes, then inspect only the feature being changed.

## Layer boundaries

- Domain owns aggregates, behavior, domain events, and entity-specific repository interfaces.
- Application owns commands/queries, handlers, inline use-case checks, DTO mapping, and application ports.
- Infrastructure owns EF repositories/configurations, JWT implementation, password-hasher binding, and technical DI registration.
- API exposure remains in `Friday.API`; do not add HTTP endpoints to this module's projects.
- Preserve project direction: Domain <- Application <- Infrastructure.

## Feature conventions

- Colocate immutable `*Command`/`*Query` records with their handlers under `Application/Features/<Area>`.
- Return DTOs/primitives directly; the current code has no `Result<T>` or validator pipeline. Do not claim either exists.
- Propagate `CancellationToken` through mediator, handler, repository, and EF calls.
- Use aggregate factories/methods for state changes and entity-specific repository ports for persistence.
- Normal command handlers defer save/commit to `TransactionBehavior` and `EfUnitOfWork`.
- Keep mappings in `Infrastructure/Persistence/Configurations` using `IEntityTypeConfiguration<T>` and the `admin` schema.
- Register repository and technical implementations in `AddAdminInfrastructure`; handlers are discovered through the Admin Application assembly marker.

## Identity and authorization safety

- Never log passwords, access/refresh tokens, hashes, signing secrets, or unnecessary user data.
- Preserve indistinguishable invalid-login responses unless the task explicitly changes the security contract.
- Account active/locked state and persisted session validity are separate checks; consider both when changing authentication.
- Rights are modeled but are not currently enforced on protected endpoints. Do not represent authentication-only Admin routes as permission-secured.
- Refresh tokens are stored as hashes and rotated in the current flow; review replay, revocation, and concurrency impact for any session change.

## Persistence and verification

- Admin uses the shared `FridayDbContext` and shared migrations; it does not own a separate database context.
- Repository writes attach/mutate tracked entities and do not normally call `SaveChangesAsync`.
- No automated Admin/auth test convention exists. For behavior changes, explicitly cover success and negative security paths when tests are part of the task, and never report unrun coverage.
