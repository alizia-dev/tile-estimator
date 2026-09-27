# ADR-001: Modular monolith, not microservices

Status: Accepted

## Context

Tile Estimator is a multi-tenant SaaS for small US tile contractors. The domain is cohesive:
takeoff, cost, pricing and quoting are steps in a single workflow that share the same catalog and
the same estimate. Traffic is low per tenant, and the team is small.

## Decision

One deployable ASP.NET Core application, organised into modules (Identity, Organizations,
Customers, Projects, Catalog, Estimation, Quotes, System) with enforced dependency rules.

## Rejected

**Microservices.** The estimate workflow crosses every module in a single request. Splitting it
would turn in-process calls into network calls and a local transaction into a distributed one,
for a workload that comfortably fits one process. The cost is paid immediately; the benefit is
hypothetical.

**A single unlayered project.** Nothing would stop a controller from doing arithmetic, and the
"one calculation engine" rule would erode within weeks.

## Consequences

- One thing to deploy, one database, real transactions.
- Module boundaries are enforced by architecture tests rather than by network topology, so they
  have to be maintained deliberately.
- If one module ever needs separate scaling, its boundary is already drawn.
