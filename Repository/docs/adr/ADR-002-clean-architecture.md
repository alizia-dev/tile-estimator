# ADR-002: Clean Architecture with enforced boundaries

Status: Accepted

## Context

The core asset is the calculation engine. It must stay correct, testable and free of incidental
dependencies for years.

## Decision

`Api -> Application -> Domain`, with Infrastructure implementing Application interfaces. The
rules are enforced by NetArchTest in `ArchitectureTests`, so a violating `using` fails the build.

Domain has **zero** EF Core references. Application references EF Core abstractions for `DbSet`
and async LINQ, but not a provider, and an architecture test enforces that distinction.

## Rejected

**Anaemic entities with logic in services.** Invariants would spread across call sites and
eventually disagree.

**Documented-but-unenforced layering.** Every codebase that does this drifts. A failing test is
worth more than a paragraph.

## Consequences

- The engines are unit-testable with no database and no mocks.
- One extra indirection for simple CRUD, accepted deliberately.
- Adding a package to the wrong project fails CI, which is the point.
