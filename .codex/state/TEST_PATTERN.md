# Friday Testing Pattern

Scope: existing automated and manual testing assets, with Login/Admin as the requested reference feature. This is an inventory of what exists; it does not propose or invent a future testing architecture.

Status vocabulary: **CONFIRMED**, **PARTIAL**, **NOT_IMPLEMENTED**, **UNKNOWN**.

## 1. Executive Finding

Automated testing in the current repository: **NOT_IMPLEMENTED**.

No test projects, automated test source files, test framework packages, test fixtures, or test commands were found outside ignored generated/build folders. Neither solution includes a test project.

Consequently, there is no existing unit, integration, database, API-host, authentication, or Login/Admin testing convention to infer.

## 2. Search Evidence

The source-first search checked:

- project and solution membership;
- filenames and directories containing `test`, `tests`, or `testing`;
- `.csproj`, central package versions, C# files, scripts, and pipeline/configuration files;
- common .NET test SDKs/frameworks and assertion libraries;
- mocking/substitution libraries;
- ASP.NET Core test hosting;
- database/container test libraries;
- test attributes and `dotnet test` commands.

No matches establishing automated tests were found for:

- `Microsoft.NET.Test.Sdk`;
- xUnit, NUnit, or MSTest;
- FluentAssertions or Shouldly;
- Moq, NSubstitute, or FakeItEasy;
- `WebApplicationFactory<TEntryPoint>` or `TestServer`;
- Testcontainers, Respawn, or WireMock;
- AutoFixture, Bogus, or equivalent test-data libraries;
- `[Fact]`, `[Theory]`, or `[Test]` test methods;
- Coverlet or another coverage collector.

## 3. Test Projects

Status: **NOT_IMPLEMENTED**.

### `Friday.slnx`

- File: `Friday.slnx`
- Contains only the API, BuildingBlocks, Admin, and Sample production projects.
- No project path is a test project.

### `src/src.sln`

- File: `src/src.sln`
- Contains the same production project set.
- No test project is included.

### Central packages

- File: `src/Directory.Packages.props`
- Contains production package versions.
- No test SDK, test framework, assertion, mocking, fixture, container, or coverage package version is declared.

## 4. Test Framework

Status: **NOT_IMPLEMENTED**.

No automated .NET test framework is referenced. Therefore the repository has no confirmed test runner, assertion API, fixture lifecycle, parameterized-test mechanism, or collection/parallelization configuration.

## 5. Unit Test Conventions

Status: **NOT_IMPLEMENTED**.

There are no unit test classes or methods from which to establish:

- arrange/act/assert style;
- system-under-test construction;
- test class organization;
- assertion conventions;
- fixture setup/teardown;
- theory/data-driven testing;
- async test conventions;
- cancellation testing;
- error/exception assertions.

Production feature-file naming and folder structure are not treated as evidence of a test convention.

## 6. Integration Test Conventions

Status: **NOT_IMPLEMENTED**.

There are no integration test projects, fixtures, test hosts, external-service fixtures, or integration-test categories/traits. No repository evidence establishes how API, EF Core, PostgreSQL, Redis, or migrations would be exercised in tests.

Docker Compose supplies development/runtime infrastructure, but no automated test harness consumes it; it is not classified as an integration-test strategy.

## 7. Database Test Strategy

Status: **NOT_IMPLEMENTED**.

No database-test fixture or lifecycle was found. Specifically, there is no confirmed:

- disposable PostgreSQL/container database;
- per-test database/schema;
- transaction rollback fixture;
- Respawn-style reset;
- migration test;
- seeded test database;
- repository integration test;
- concurrency test;
- relational constraint/index test.

The production infrastructure references EF Core InMemory and falls back to `Friday.Shared` when `ConnectionStrings:FridayDb` is absent. This is production configuration behavior, not evidence of a database test strategy, because no test consumes it.

Evidence:

- File: `src/BuildingBlocks/Friday.BuildingBlocks.Infrastructure/Persistence/RelationalDbContextConfigurer.cs`
- Class/method: `RelationalDbContextConfigurer.Configure`
- Purpose: runtime provider selection, including missing-connection in-memory fallback.

## 8. WebApplicationFactory Usage

Status: **NOT_IMPLEMENTED**.

No `WebApplicationFactory<TEntryPoint>`, `Microsoft.AspNetCore.Mvc.Testing`, `TestServer`, custom API factory, or test-host environment setup exists in the searched repository source/configuration.

## 9. Mocking Strategy

Status: **NOT_IMPLEMENTED**.

No mocking framework or hand-written test doubles were found in a test context. The Sample module's `InMemoryTodoItemRepository` is a registered production implementation and is not treated as a mock, fake, or test fixture.

There is therefore no established convention for mocking repositories, the Unit of Work, JWT issuance, options, clocks, caches, HTTP context, or CQRS dispatch.

## 10. Test Naming Convention

Status: **NOT_IMPLEMENTED**.

No test class or method names exist from which to infer conventions such as:

- `Method_State_ExpectedResult`;
- `Given_When_Then`;
- behavior-style sentences;
- `Should...` naming;
- test project/namespace suffixes.

Any naming recommendation would be speculative and is intentionally omitted.

## 11. Authentication Test Setup

Status: **NOT_IMPLEMENTED**.

No automated authentication fixture exists. There is no confirmed test setup for:

- issuing or injecting test JWTs;
- overriding JWT bearer authentication;
- authenticated `ClaimsPrincipal` creation;
- active/revoked session setup;
- active, inactive, or locked users;
- role claims;
- unauthorized and forbidden responses;
- invalid signature, issuer, audience, or expiry;
- refresh-token rotation/replay;
- logout/session revocation.

Production JWT configuration and authentication code are not classified as test setup.

## 12. Test Data Creation

Status: **NOT_IMPLEMENTED**.

No builders, object mothers, factories, fixtures, seed helpers, generated data, snapshots, or checked-in test datasets were found.

Production FluentMigrator localization seeds are application data migrations, not test-data creation. Docker Compose database credentials and runtime services are also not test fixtures.

## 13. Login/Admin Reference Feature

Login/Admin automated tests: **NOT_IMPLEMENTED**.

No tests exist for the Login/Admin feature's:

- endpoint binding or response envelope;
- user lookup by username/email/code;
- password verification;
- inactive or locked user rejection;
- role loading and role claims;
- session creation;
- JWT creation/validation;
- invalid-credential behavior;
- command transaction commit/rollback;
- database queries and persistence;
- refresh rotation/replay;
- logout/revocation;
- protected Admin endpoints;
- role/right authorization behavior.

Because no Login/Admin tests exist, this feature cannot serve as an existing test pattern. Its production implementation was not reanalyzed for this task.

## 14. Manual HTTP Artifact

Status: **PARTIAL manual request aid; not an automated test**.

- File: `src/API/Friday.API/Friday.API.http`
- Defines `Friday.API_HostAddress = http://localhost:5035`.
- Sends one GET request to `/weatherforecast/` with `Accept: application/json`.
- Contains no assertions, setup, authentication, Login/Admin request, data cleanup, or repeatable pass/fail mechanism.
- Current `Program.cs` does not map `/weatherforecast/`; the mapped root and module endpoints use other routes.

This file appears stale relative to current endpoint mappings and does not establish a testing convention.

## 15. CI and Automated Test Execution

Status: **NOT_IMPLEMENTED** in the searched repository content.

No CI workflow/pipeline or repository script invoking `dotnet test` or collecting coverage was found. The `.codex/agents/test-verifier.toml` file contains instructions for an AI verification agent, but it does not define or provide a test suite and is not application test infrastructure.

## 16. Coverage by Requested Area

| Requested area | Status | Evidence summary |
|---|---|---|
| Test projects | **NOT_IMPLEMENTED** | Neither solution includes one; none found by filename/project search. |
| Test framework | **NOT_IMPLEMENTED** | No test SDK/framework package or attributes. |
| Unit test conventions | **NOT_IMPLEMENTED** | No unit tests. |
| Integration test conventions | **NOT_IMPLEMENTED** | No integration tests or fixtures. |
| Database test strategy | **NOT_IMPLEMENTED** | No database fixture/reset/container/migration tests. |
| WebApplicationFactory | **NOT_IMPLEMENTED** | No MVC testing package or factory usage. |
| Mocking strategy | **NOT_IMPLEMENTED** | No mocking libraries or test doubles in test context. |
| Test naming convention | **NOT_IMPLEMENTED** | No test names to analyze. |
| Authentication test setup | **NOT_IMPLEMENTED** | No auth test host, tokens, principals, sessions, or cases. |
| Test data creation | **NOT_IMPLEMENTED** | No test builders/fixtures/seeds/generators. |
| Login/Admin tests | **NOT_IMPLEMENTED** | No feature tests found. |
| Manual HTTP requests | **PARTIAL** | One stale, assertion-free `/weatherforecast/` request. |

## 17. Unknowns and Limits

- Tests that may exist outside this repository/workspace: **UNKNOWN**.
- Tests supplied only by an external CI system or private package: **UNKNOWN**.
- Historical tests removed from the current checkout: **UNKNOWN**.
- Runtime behavior was not tested because the task requested analysis of the existing approach, and no automated suite exists to run.

Within the current repository, the requested automated testing areas are **NOT_IMPLEMENTED**, not merely undocumented.
