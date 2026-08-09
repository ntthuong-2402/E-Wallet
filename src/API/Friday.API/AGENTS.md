# Friday.API Instructions

Apply these rules with the repository-root `AGENTS.md`.

## Scope

This project is the ASP.NET Core composition and transport boundary. For established behavior, consult `.codex/state/API_PATTERN.md` and `.codex/state/CONFIGURATION_PATTERN.md`; do not rediscover unrelated modules.

## Current boundaries

- Keep endpoints as thin Minimal API adapters: bind transport input, dispatch through `IMediator`, and format the response.
- Do not place repositories, EF queries, DbContext access, or business rules in API endpoint files.
- Commands use `SendAsync`; queries use `QueryAsync`; propagate the request `CancellationToken`.
- Use the existing `ApiResults`/`ApiResponse` envelope for application-created responses. Do not introduce another response format incidentally.
- Expected feature failures flow through `FridayException` and `ExceptionHandlingMiddleware`; never expose internal exception details.
- Treat authorization metadata explicitly. Authentication alone is not proof of role or permission authorization.

## Composition and pipeline

- `Program.cs` is the composition root. Register module extensions and map endpoint groups explicitly.
- Preserve middleware ordering unless the task specifically changes pipeline semantics; verify authentication, authorization, exception handling, correlation, and request logging after any reorder.
- Custom middleware currently uses conventional `UseMiddleware<T>` activation.
- Never put secrets in appsettings, Docker configuration, logs, examples, or committed HTTP requests.
- When adding startup configuration, identify its environment override and validation behavior.

## Verification

- Check route, status, response envelope, authentication/authorization, and failure behavior.
- No automated API test pattern currently exists. Do not claim test coverage that is absent; if tests are requested, follow the root testing workflow and record the newly established convention.
