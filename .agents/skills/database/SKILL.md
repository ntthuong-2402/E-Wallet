---
name: database
description: Use for PostgreSQL schema/migrations, EF Core mappings, indexes, constraints, query plans, concurrency, pagination, data retention, and measured Dapper/raw-SQL optimization.
---


# Database Workflow

1. Confirm the owning bounded context.
2. Choose precise types: `numeric` precision/scale for money, UTC timestamps, explicit currency.
3. Add database constraints for invariants that must survive application bugs.
4. Design uniqueness around idempotency and business references.
5. Add indexes from real query patterns, not guesswork.
6. Avoid cross-service foreign keys/joins.
7. Treat migrations as deployable artifacts; consider backward compatibility and forward-fix/rollback strategy.
8. For performance, inspect actual query plans before introducing Dapper/raw SQL.

Read `references/DATA_OWNERSHIP.md` when designing/changing tables.

