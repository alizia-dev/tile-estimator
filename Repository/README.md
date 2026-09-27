# Tile Estimator

A multi-tenant SaaS for US tile contractors: takeoff → estimate → quote → customer approval.

Built to `docs/SPEC.md`. The MVP is **100% deterministic and non-AI** — every quantity and every
dollar comes from a tested calculation engine, and an architecture test fails the build if an AI
or ML package is ever added.

---

## What it does

Register → create an organization → add customers → create a project → lay out rooms and
surfaces → pick tile, pattern and assembly → calculate tile, materials and labor → apply
overhead, markup and tax → produce an estimate → finalize it → convert to a quote → generate a
PDF → send it → the customer accepts from a private link → the whole thing is audited.

Plus five quick calculators (floor, wall, backsplash, shower, bathroom) that call the same engine
as a full estimate, so their answers always agree.

---

## Stack

**Backend** .NET 10 · ASP.NET Core · EF Core 10 · SQL Server · FluentValidation · Serilog ·
QuestPDF · xUnit · NetArchTest

**Frontend** Angular 22 (standalone, signals, zoneless) · TypeScript strict · Angular Material ·
Vitest

---

## Layout

```
backend/      TileEstimator.slnx, src/ and tests/
front-end/    the Angular app
docs/         architecture, engines, tenancy, API, ADRs
```

See [docs/architecture.md](docs/architecture.md).

---

## Running it

### Prerequisites

- .NET 10 SDK
- Node 20+
- SQL Server — LocalDB is fine (`(localdb)\MSSQLLocalDB`)

### Backend

```bash
cd backend

# Set a real JWT secret for local development
dotnet user-secrets set "Jwt:Secret" "<64+ random characters>" \
  --project src/TileEstimator.Api

dotnet build TileEstimator.slnx
dotnet run --project src/TileEstimator.Api
```

The API starts on `http://localhost:5199`, with Swagger at `/swagger`.

In Development, `Database:AutoMigrate` and `Database:SeedDemoData` are on, so it creates the
schema, seeds roles and permissions, and creates a demo organization:

```
Demo Tile Company
demo@tileestimator.local  /  Demo123!Pass
```

Emails are not sent anywhere. The development sender writes them to
`src/TileEstimator.Api/outbox/`, which is where you find the verification and quote links.

### Frontend

```bash
cd front-end
npm ci
npm start          # http://localhost:4200
```

---

## Testing

```bash
# Backend: 90 tests
cd backend && dotnet test TileEstimator.slnx

# Frontend: 30 tests
cd front-end && npx ng test --watch=false
```

| Suite | Covers |
|---|---|
| `UnitTests` | the engines — areas, openings, waste, boxes, coverage, both labor methods, markup vs margin, discounts, tax basis, ordering, and the §14 reference case |
| `IntegrationTests` | tenant isolation against a real DbContext with filters and interceptor |
| `ArchitectureTests` | layering, no EF in Domain, no AI packages, no `double` for money |

The reference case from SPEC §14 is pinned in both the unit tests and an end-to-end run:
12 × 15 → 180 SF → 12% waste → 201.6 SF → 10 SF/box → 20.16 → **21 boxes**.

---

## Database

```bash
cd backend
dotnet ef migrations add <Name> -p src/TileEstimator.Infrastructure -s src/TileEstimator.Api
dotnet ef database update  -p src/TileEstimator.Infrastructure -s src/TileEstimator.Api
```

---

## Configuration

Strongly typed and validated at startup, so a missing setting fails the deployment rather than
the first request.

| Section | Key settings |
|---|---|
| `Jwt` | `Secret` (32+ chars, from user-secrets or env), `AccessTokenMinutes`, `RefreshTokenDays` |
| `Database` | `ConnectionString`, `AutoMigrate`, `SeedDemoData` |
| `Storage` | `RootPath`, `MaxUploadMegabytes` |
| `Email` | `FromAddress`, `OutboxPath` |
| `Application` | `WebAppBaseUrl`, `CorsOrigins`, token lifetimes, lockout |

Environment variables use `__`, e.g. `Jwt__Secret`, `Database__ConnectionString`.

**No secret is committed.** The development JWT secret in `appsettings.Development.json` is
labelled as such and must not be used anywhere else.

---

## How the important parts work

- **Tenant isolation** is structural: EF global query filters plus a SaveChanges interceptor. An
  `OrganizationId` from a client is never trusted. → [docs/multi-tenancy.md](docs/multi-tenancy.md)
- **One calculation engine.** Pure, synchronous, no database. Estimates and every quick
  calculator call it. → [docs/estimation-engine.md](docs/estimation-engine.md)
- **Snapshots, not references.** Estimate lines store the price, waste and formula used, so
  changing a catalog price never moves a finalized estimate.
  → [docs/pricing-engine.md](docs/pricing-engine.md)
- **Finalized estimates are immutable.** Edits create a new version; financial records are never
  hard-deleted.
- **Money is `decimal`,** with one rounding policy, enforced by an architecture test.

---

## Documentation

| Document | |
|---|---|
| [architecture.md](docs/architecture.md) | structure, boundaries, pipeline |
| [estimation-engine.md](docs/estimation-engine.md) | every formula, the rounding policy, worked examples |
| [pricing-engine.md](docs/pricing-engine.md) | markup vs margin, order of operations, tax basis |
| [multi-tenancy.md](docs/multi-tenancy.md) | how isolation is enforced and tested |
| [authorization.md](docs/authorization.md) | the role → permission matrix |
| [api.md](docs/api.md) | every endpoint, permission and status code |
| [decisions-log.md](docs/decisions-log.md) | choices where the spec left room |
| [adr/](docs/adr/) | ADR-001 … ADR-011 |

---

## Known gaps

- **CSV catalog import/export** (§10) — the abstraction boundary exists; the endpoint and preview
  UI are not built.
- **Change orders** (§17) — modelled and persisted, as the spec asks; the workflow is V2.
- **Supplier price lists** — entities and schema exist; no management UI.
- **Report exports** — the reports page summarises and links through; PDF/CSV export of the eight
  §21 reports is not implemented.
- **Document upload UI** — `IFileStorageProvider` and `ProjectDocument` exist; no upload screen.
- **Playwright UI tests** — the end-to-end workflow is covered at the API level.

Two decisions affecting money are flagged for product-owner confirmation in
[decisions-log.md](docs/decisions-log.md): how tax is apportioned across materials and labor, and
whether tile is billed by whole boxes or installed square footage.
