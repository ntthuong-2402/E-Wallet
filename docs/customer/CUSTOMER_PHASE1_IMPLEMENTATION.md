# Customer Phase 1 Implementation

Status: Implemented
Date: 2026-08-11

## Delivered boundary

- Three Customer projects: Domain, Application, and Infrastructure.
- Explicit inward project-reference direction.
- Customer included in Friday CQRS discovery and API composition.
- Dedicated `CustomerDbContext` with default schema `customer`.
- Dedicated `customer.__EFMigrationsHistory` and `CustomerBaseline` migration.
- Dedicated `ICustomerUnitOfWork` and keyed transaction routing.
- Separate `ConnectionStrings:CustomerDb` runtime configuration.
- Separate `FRIDAY_CUSTOMER_DESIGN_TIME_PG` design-time override.
- Customer startup migration hook under the existing global migration switch.

## Transaction routing

Legacy commands that implement only `ICommand<TResponse>` continue using the
shared `IUnitOfWork` and `FridayDbContext`.

Customer commands implement `ICustomerCommand<TResponse>`. That marker exposes
the Customer unit-of-work key, and the shared transaction behavior resolves the
keyed `ICustomerUnitOfWork` for that command.

```text
legacy ICommand<T>
  -> TransactionBehavior
  -> default IUnitOfWork
  -> FridayDbContext

ICustomerCommand<T>
  -> TransactionBehavior
  -> keyed Customer IUnitOfWork
  -> CustomerDbContext
```

This prevents multiple DbContext registrations from relying on DI registration
order and preserves existing Admin command behavior.

## Migration ownership

The baseline migration creates only the Customer schema. It deliberately does
not create a Customer business table; aggregate/schema work belongs to Phase 2.

Verified generated SQL order:

1. create schema `customer` when absent;
2. create `customer.__EFMigrationsHistory`;
3. record `CustomerBaseline`.

## Verification

- Full solution build passed with zero errors.
- Customer unit tests: 5 passed.
- Existing Admin unit tests: 4 passed.
- Existing API integration tests: 7 passed.
- Customer migration discovered by `dotnet-ef`.
- EF reports no pending Customer model changes.
- Docker Compose configuration validation passed, with the existing local
  Docker config access warning.

## Deferred to Phase 2+

- Customer aggregate, profile schema, CustomerCode generator, CitizenId
  protection, and audit persistence were delivered in Phase 2; see
  `CUSTOMER_PHASE2_IMPLEMENTATION.md`.
- Customer APIs and permissions remain deferred.
- Identity linkage.
