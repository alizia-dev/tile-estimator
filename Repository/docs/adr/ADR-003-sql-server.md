# ADR-003: SQL Server with EF Core

Status: Accepted

## Context

The system handles money and needs exact decimal arithmetic, real foreign keys, unique and check
constraints, and optimistic concurrency. The target deployment is Azure.

## Decision

SQL Server (Azure SQL in production, LocalDB for development), accessed through EF Core 10 with
code-first migrations.

`decimal(18,2)` for money, `decimal(18,4)` for quantities, rates and percentages.

## Rejected

**PostgreSQL.** A fine database; SQL Server simply matches the Azure target and the .NET tooling
more closely here. The provider is isolated in Infrastructure, so the decision is reversible.

**A document store.** Estimates, quotes, projects and the catalog are deeply relational, and the
integrity rules that protect financial data are exactly what a relational engine provides.

**Dapper or raw SQL.** EF Core global query filters and the SaveChanges interceptor are what make
tenant isolation structural. Hand-written SQL would make it a thing to remember.

## Consequences

- Exact money arithmetic, enforced at the schema level as well as in code.
- Migrations are reviewable and versioned.
- Query filters and the interceptor give isolation by construction.
