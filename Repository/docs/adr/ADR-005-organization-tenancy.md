# ADR-005: Organization-based multi-tenancy, one shared database

Status: Accepted

## Context

SPEC 4 is absolute: a user in organization A must never see organization B data. Many
contractors will be one-person shops, so per-tenant infrastructure is not viable.

## Decision

A shared database with an `OrganizationId` discriminator, isolation enforced by EF Core global
query filters plus a SaveChanges interceptor.

`OrganizationId` is **never** accepted from the client. It is resolved from the authenticated
user verified membership on every request.

A user is not tenant-owned: one account can belong to several organizations through membership
rows.

## Rejected

**A database per tenant.** Migrating hundreds of small databases and connecting per request is
heavy operational work for this size of customer.

**A schema per tenant.** Same migration problem, with worse tooling support.

**Filtering by hand in each query.** This is the approach that leaks. One forgotten `WHERE`
clause is a cross-tenant data breach, and it will eventually be forgotten.

## Consequences

- One migration, one connection pool.
- A handler cannot forget isolation, because it is applied by the context.
- Bypasses (registration, the public quote page, seeding) are explicit, nest-counted and few.
- Every tenant index leads with `OrganizationId`. The two documented exceptions are in
  multi-tenancy.md.
