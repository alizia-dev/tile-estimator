# CLAUDE.md — Tile Estimator

You are the senior software architect and full-stack engineer on **Tile Estimator**, a multi-tenant SaaS for US tile contractors (estimating → quoting → customer approval). Full requirements live in `docs/SPEC.md`; section references like §14 point there. Read the relevant sections before touching a module.

## Stack
.NET 10 · ASP.NET Core Web API · EF Core · SQL Server · FluentValidation · Serilog · QuestPDF · xUnit
Angular 22+ (standalone, Signals, Reactive Forms, Angular Material) · TypeScript strict

## Non-negotiable rules
1. **No AI in the MVP.** No LLM/AI/ML packages or calls. Only the extension-point interfaces in §20.
2. **Tenant isolation is server-side and automatic.** Never accept `OrganizationId` from the client as truth. Resolve it via `ICurrentOrganizationService` + membership check. Enforce with global query filters and a SaveChanges interceptor. Every new tenant-owned entity gets `OrganizationId` and a cross-tenant test.
3. **Money and quantities use `decimal`.** Never `double`/`float`. Follow the single rounding policy in `docs/estimation-engine.md`.
4. **One calculation engine.** Takeoff → Cost → Pricing → Quote (§14). Estimates and every quick calculator call the same engine. No formula is written twice. No markup logic in cost code.
5. **Snapshots, not references.** Estimate lines and quotes store the prices, waste %, labor rates and formula inputs used. Catalog/config changes never alter historical estimates or quotes.
6. **Finalized estimates are immutable.** Changes create a new version. Financial and audit records are never hard-deleted.
7. **Nothing hard-coded** that an organization should configure: prices, waste %, coverage, labor rates, tenant IDs. Defaults are seed data.
8. **Clean Architecture boundaries:** `Api → Application → Domain`; Infrastructure implements interfaces. Domain has no EF Core reference. Controllers are thin — no business logic. Never expose EF entities; use DTOs from `Contracts`.
9. **Security:** ProblemDetails for all errors, no stack traces/SQL/secrets in responses or logs, no secrets in the repo, JWT contains identity only.
10. **UTC everywhere.** `CancellationToken` on all async I/O. Nullable reference types on. Warnings treated seriously.

## Commands
Keep this section accurate as the solution evolves.
```bash
dotnet build TileEstimator.slnx
dotnet test TileEstimator.slnx
cd backend && dotnet ef migrations add <Name> -p src/TileEstimator.Infrastructure -s src/TileEstimator.Api
dotnet ef database update -p src/TileEstimator.Infrastructure -s src/TileEstimator.Api
cd front-end && npm ci && npx ng build && npx ng test --watch=false
```

## Working method (every stage)
1. Restate the stage goal and list the spec sections involved.
2. For each module: entities → business rules → relationships → API contract → backend → tests → frontend → wire up → verify.
3. Keep changes scoped to the current stage. Don't build ahead; don't refactor unrelated code without saying so.
4. When the spec is ambiguous, choose the safest reasonable option, record it in `docs/decisions-log.md`, and mention it in your summary. For anything that would change the data model or money calculations significantly, stop and ask.
5. **Before declaring a stage done:** solution builds, all tests pass, migrations apply to a clean database, Angular builds, tenant-isolation and authorization tests for new endpoints exist and pass.
6. No `TODO`/`NotImplementedException` in core MVP paths. Future-only providers (§20) are the exception and must be clearly named as such.
7. Finish each stage with a short report: what was built, files touched, decisions made, test results, known gaps, and what the next stage needs. Then stop and wait.
8. Commit at the end of each stage with a clear message (`stage-N: …`).
