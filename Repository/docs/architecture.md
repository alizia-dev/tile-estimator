# Architecture

Clean Architecture, modular monolith. No microservices (see [ADR-001](adr/ADR-001-modular-monolith.md)).

---

## Solution structure

```
backend/
  TileEstimator.slnx                  (.NET 10 uses the .slnx solution format)
  Directory.Build.props               nullable, implicit usings, analyzers
  src/
    TileEstimator.Domain/             entities, value objects, invariants — no EF Core
    TileEstimator.Contracts/          request/response DTOs
    TileEstimator.Application/        engines, services, abstractions
    TileEstimator.Infrastructure/     EF Core, providers, PDF, seeding
    TileEstimator.Api/                controllers, middleware, policies
  tests/
    TileEstimator.UnitTests/          the engines
    TileEstimator.IntegrationTests/   tenancy and persistence
    TileEstimator.ApiTests/
    TileEstimator.ArchitectureTests/  the rules on this page, enforced
front-end/
  src/app/
    core/        auth, guards, interceptors, services, models
    shared/      shell, pipes, directives, dialogs
    features/    dashboard, customers, projects, catalog, estimates,
                 quotes, calculators, reports, settings, auth, public-quote
docs/
```

---

## Dependency direction

```
Api ──▶ Application ──▶ Domain
 │           ▲
 └──▶ Infrastructure ──┘   (implements Application's interfaces)

Contracts is referenced by Application and Api; it depends on nothing.
```

Enforced by `ArchitectureTests`, which fail the build if:

- Domain references EF Core, ASP.NET Core, Infrastructure, Application or Api;
- Application references Infrastructure, Api, or a concrete database provider;
- anything in `Application.Engines` references EF Core or Infrastructure;
- a controller references the concrete `ApplicationDbContext` instead of `IApplicationDbContext`;
- any project references an AI or ML package (CLAUDE.md rule 1);
- a tenant-owned entity lacks an `OrganizationId`;
- a money-or-quantity property is `double` or `float`.

**On EF Core in Application.** Application references `Microsoft.EntityFrameworkCore` for `DbSet`
and the async LINQ operators, which is the usual Clean Architecture compromise. It does *not*
reference the SQL Server provider — choosing a database is an infrastructure decision, and an
architecture test enforces the distinction. Domain has zero EF references of any kind.

---

## The four engines

One direction only, each answering one question:

```
Takeoff  →  "How much material is required?"      quantities only
   ↓
Cost     →  "What does the job cost us?"          no markup logic
   ↓
Pricing  →  "What do we charge?"                  markup, margin, discount, tax
   ↓
Quote    →  "What does the customer approve?"     snapshot
```

They are pure, synchronous and fully unit-tested. Every quantity and every dollar in the product
comes from them, including the quick calculators — a calculator endpoint builds a detached
`Surface`, passes it through the *same* `TakeoffBuilder` a project uses, and hands the result to
the *same* engine. No formula exists twice.

---

## Request pipeline

```
CorrelationIdMiddleware        attach/echo X-Correlation-Id
ExceptionHandlingMiddleware    everything → RFC 7807 ProblemDetails
Serilog request logging        enriched with correlation and organization
Security headers               nosniff, DENY, no-referrer, restrictive CSP
CORS                           configured origins only
Rate limiter                   auth 10/min/IP, public quote 30/min/IP
Authentication                 JWT bearer, zero clock skew
TenantResolutionMiddleware     resolve + verify membership  ← SPEC §4
Authorization                  permission policies, authenticated fallback
Controllers
```

---

## Cross-cutting concerns

| Concern | Where |
|---|---|
| Tenant isolation | query filters + save interceptor ([multi-tenancy.md](multi-tenancy.md)) |
| Auditing | `IAuditService`, with secret redaction |
| Errors | `ExceptionHandlingMiddleware` → ProblemDetails with `traceId` |
| Logging | Serilog, console + rolling file |
| Concurrency | `rowversion` on Estimate, Quote, PriceList, ChangeOrder |
| Soft delete | `ISoftDeletable`, applied by the interceptor |
| Numbering | `NumberSequenceService`, row-locked per organization |
| Config | strongly typed options, validated on start |

---

## Controllers are thin

A controller resolves input, calls a service or engine, maps to a DTO and returns. Business rules
live in the domain and in `Application/Services`. EF entities are never returned: everything goes
through `Api/Mapping/Mappers.cs`.

---

## Future AI extension points (§20)

`IDocumentTakeoffProvider`, `ITakeoffExtractionProvider` and `IExternalPricingProvider` are
declared and registered, with explicit "not available" implementations. They exist so the
composition root and call sites are already shaped for V4.

There is no AI, LLM or ML anywhere in this MVP, and an architecture test keeps it that way.

When AI does arrive it is an **input provider only**: it proposes measurements a human reviews,
which then go through the same deterministic engines. It never produces money and never bypasses
them.
