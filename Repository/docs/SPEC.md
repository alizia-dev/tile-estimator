# Tile Estimator — Product & Technical Specification (MVP)

This is the source of truth for requirements. Section numbers (§) are referenced from `CLAUDE.md` and the stage prompts.

---

## §1 Product

A production-ready, multi-tenant SaaS for US tile contractors, estimators, construction companies and flooring/tile businesses. It is a professional estimating and quick-quote platform, not a simple tile calculator.

The MVP is **100% deterministic and non-AI**. No OpenAI/Claude/LLMs, computer vision, agents, AI PDF takeoff or ML. The architecture must let AI/document takeoff be added later without significant changes to the domain, estimation engine, database or public APIs (§20).

### Core workflow a contractor must be able to do
Register → create organization → invite/manage users → create customers → create projects → create rooms/zones → define floor/wall/shower/backsplash surfaces → select tile → select pattern → configure waste → calculate tile, materials and labor → apply overhead → apply markup or margin → apply discount and tax → generate estimate → convert to quote → generate PDF proposal → send quote → track status → customer approves → audit history recorded.

### Engines (independent, one direction only)
```
Takeoff Engine  → "How much material is required?"
      ↓
Cost Engine     → "What does the job cost us?"
      ↓
Pricing Engine  → "What do we charge?" (markup/margin/discount/tax)
      ↓
Quote Engine    → "What does the customer see and approve?" (snapshot)
```

---

## §2 Technology stack

**Backend:** .NET 10, ASP.NET Core Web API, C#, EF Core, SQL Server, FluentValidation, Serilog, Swagger/OpenAPI, JWT (OIDC-compatible design), refresh tokens, policy-based ASP.NET Core authorization, REST.

**Frontend:** Angular 22+ (latest stable), TypeScript (strict), standalone components, Signals, Router with lazy-loaded feature routes, Reactive Forms, Angular Material, responsive (desktop-first, tablet-friendly).

**Database:** SQL Server, EF Core migrations, real FKs, indexes, unique/check constraints, soft delete where appropriate, `rowversion` concurrency on financial records.

**PDF:** QuestPDF (or equivalent .NET library; confirm license terms in an ADR).

**Infrastructure abstractions (interfaces only, simple local implementations in MVP):** file storage, email, background jobs, notifications, payments, future AI provider. Do not add external services the MVP doesn't need.

---

## §3 Architecture

Clean Architecture, **modular monolith** (no microservices).

```
TileEstimator.sln
src/
  TileEstimator.Api/
  TileEstimator.Application/
  TileEstimator.Domain/
  TileEstimator.Infrastructure/
  TileEstimator.Contracts/        # request/response DTOs shared by API
tests/
  TileEstimator.UnitTests/
  TileEstimator.IntegrationTests/
  TileEstimator.ApiTests/
frontend/
  tile-estimator-web/
docs/
```

Dependency direction: `Api → Application → Domain`. Infrastructure implements Application/Domain interfaces. Domain has no dependency on Infrastructure or EF Core. Application does not reference EF Core implementations directly.

### Modules
| Module | Entities |
|---|---|
| Identity | User, Role, Permission, RefreshToken, EmailVerification |
| Organizations | Organization, OrganizationMember, OrganizationSettings |
| Customers | Customer, Contact, Address |
| Projects | Project, Room, Surface (zone), Opening, ProjectNote, ProjectDocument |
| Catalog | Tile, Material, Supplier, SupplierProduct, PriceList, PriceListItem |
| Estimation | Takeoff, TakeoffItem, Assembly, AssemblyItem, Pattern, WasteRule, LaborRate, Estimate, EstimateLine |
| Quotes | Quote, QuoteLine, QuoteRecipient, QuoteApproval, ChangeOrder |
| System | AuditLog, Notification, ApplicationSetting |

---

## §4 Multi-tenancy

- Every tenant-owned entity has `OrganizationId` (Project, Customer, Estimate, Quote, Tile, Material, LaborRate, PriceList, Assembly, etc.).
- **Never trust an `OrganizationId` sent by the client.** Resolve the current organization server-side from the authenticated user and their verified membership. If a user can belong to several organizations, the client may *select* one, but the server must validate membership on every request.
- Abstractions: `ICurrentUserService`, `ICurrentOrganizationService`.
- Isolation is enforced automatically (e.g. EF Core global query filters + a SaveChanges interceptor that stamps `OrganizationId` and rejects cross-tenant writes), not by remembering to add a `WHERE` clause. Conceptually every query is `WHERE Id = @id AND OrganizationId = @currentOrgId`.
- A user in Org A must never read or modify Org B data. This is covered by automated tests (§22).

---

## §5 Authentication

Registration, login, logout, refresh, password hashing (ASP.NET Core Identity hasher or equivalent), email verification (via abstraction), forgot/reset password, account activation/deactivation.

- Short-lived access tokens; rotating, revocable refresh tokens stored hashed.
- JWT carries only necessary identity claims — no business data.

**Registration form:** First Name, Last Name, Email, Password, Confirm Password, Company Name, Accept Terms.

**Registration transaction:** create user → create organization → create membership → assign Owner role → create default OrganizationSettings → create default estimation configuration (patterns, waste rules, material categories, room types) → send verification email via abstraction → redirect to dashboard.

---

## §6 Authorization

RBAC with granular permissions and policy-based authorization.

Roles: Owner, Admin, Estimator, Sales, Installer, Viewer.

Permissions (initial):
```
project.read  project.create  project.update  project.delete
estimate.read estimate.create estimate.update estimate.delete estimate.approve
quote.read    quote.create    quote.send      quote.approve
catalog.read  catalog.manage
users.read    users.invite    users.manage
audit.read
```
Produce a role→permission matrix in `docs/authorization.md`.

---

## §7 Dashboard

KPIs: total projects, active projects, draft estimates, quotes sent / accepted / rejected, total quoted value, accepted quote value, average estimate value. Lists: recent projects, estimates, quotes. Quote pipeline: Draft → Sent → Viewed → Accepted / Rejected / Expired.

---

## §8 Customers

Fields: Id, OrganizationId, CustomerNumber, FirstName, LastName, CompanyName, Email, Phone, Notes, BillingAddress, ServiceAddress, Status, CreatedAt, UpdatedAt. Supports residential and commercial customers.

---

## §9 Projects, rooms, surfaces

**Project fields:** Id, OrganizationId, CustomerId, ProjectNumber, Name, Description, ProjectType, Address, Status, StartDate, EstimatedCompletionDate, CreatedBy, CreatedAt, UpdatedAt.

**Project statuses:** Draft, Estimating, EstimateReady, Quoted, Accepted, Scheduled, InProgress, Completed, Cancelled, Closed.

**Room types:** Bathroom, Master Bathroom, Kitchen, Living Room, Bedroom, Shower, Backsplash, Entry, Laundry, Custom.

**Surface types (many per room):** Floor, Wall, Shower Floor, Shower Wall, Backsplash, Custom Surface. Surfaces can have openings (doors, windows, niches, etc.).

**Project page tabs:** Overview, Rooms, Takeoff, Estimate, Quote, Documents, Notes, Activity. Overview shows project info, customer, status, total estimate, quote status, recent activity.

---

## §10 Catalog

**Tile:** Id, OrganizationId, SKU, Brand, ProductName, Collection, MaterialType, LengthInches, WidthInches, ThicknessInches, CoverageSqFt, TilesPerBox, SqFtPerBox, CostPerSqFt, SellingPricePerSqFt, Finish, Color, Description, Active.
MaterialType: Ceramic, Porcelain, Natural Stone, Glass, Mosaic, Marble, Granite, Travertine, Slate, Other.

**Material:** Id, OrganizationId, SKU, Name, Category, Unit, Coverage, Cost, SellingPrice, SupplierId, Active.
Categories include Thinset, Grout, Backer Board, Waterproofing Membrane, Sealer, Caulk, Primer, Adhesive, Trim, Bullnose, Spacers.

**Suppliers:** Supplier, SupplierProduct, PriceList, PriceListItem — organization-specific pricing. No external supplier APIs. CSV import/export behind an abstraction (interface + basic implementation).

All catalog data is organization-specific.

---

## §11 Patterns and waste

**Pattern:** Id, Name, Description, DefaultWastePercentage, Active. Initial: Straight Lay, Grid, Running Bond, Brick, Diagonal, Herringbone, Stacked, Custom.

**WasteRule:** configurable; may depend on pattern, surface type, tile type, room type and organization settings.

Resolution: `pattern default → matching waste rule → estimator override → final waste`.

- No hard-coded "industry standard" waste. All defaults are seed data the organization can edit.
- The **final applied waste % is stored** on the takeoff/estimate line so history never changes when configuration changes.

---

## §12 Assemblies

An assembly is a complete, organization-configurable installation system.

Examples (seed data): Standard Floor (tile, thinset, grout, labor); Bathroom Floor (+ sealer); Shower (wall tile, floor tile, thinset, grout, backer board, waterproofing, trim, caulk, labor); Backsplash (tile, adhesive/thinset, grout, caulk, trim, labor).

**AssemblyItem:** material, quantity calculation method (per area, per linear ft, per each, per coverage), waste rule, unit, labor relationship.

---

## §13 Labor rates

**LaborRate:** Id, OrganizationId, Name, Trade, Unit, Rate, Productivity, Active. Examples: Tile Installation, Backer Board Installation, Waterproofing, Demolition, Grouting, Trim Installation.

Two methods:
- `Quantity × UnitRate`
- `Quantity ÷ Productivity × HourlyRate`

Store calculation details on the line for auditability (e.g. `450 SF × $8.00/SF = $3,600.00`).

---

## §14 Calculation engines

All deterministic, pure, synchronous, fully unit-tested. No AI, LLM or external calls. Each rule is implemented **once** and reused by estimates and every quick calculator.

### ITakeoffEngine
Surface areas, openings, waste, tiles, boxes, linear feet, material quantities.

```
Floor area       = Length × Width
Wall area        = Length × Height          (sum across walls)
Opening area     = Width × Height
Net area         = Gross area − Σ openings
Adjusted area    = Net area × (1 + Waste%)
Boxes (calc)     = Adjusted area ÷ SqFtPerBox
Boxes (purchase) = ceiling(Boxes calc)
Material qty     = Area ÷ coverage per unit  (thinset, grout, backer board, waterproofing…)
```
Always store **both** calculated quantity and rounded purchase quantity. Coverage values come from the org's catalog or assembly — never hard-coded.

**Reference test:** 12 × 15 → 180 SF; waste 12% → 201.6 SF; 10 SF/box → 20.16 → **21 boxes**.

### ICostCalculationEngine
Material, labor, equipment, delivery, other costs, overhead. No markup logic here.

### IPricingEngine
Markup %, gross margin %, fixed markup, discount, tax. Markup and margin are different and the strategy used is stored:
```
Markup: Price = Cost × (1 + Markup)
Margin: Price = Cost ÷ (1 − Margin)     (Margin < 1)
```

### IQuoteEngine
Converts a finalized estimate into a quote containing a full pricing snapshot.

### Money rules
- `decimal` only — never `double`/`float` for money or quantities that feed money.
- Money is explicit about currency, unit, precision and rounding. Default currency USD; currency configurable per organization for the future.
- DB: `decimal(18,2)` for money, higher precision (e.g. `decimal(18,4)`) for quantities, rates and percentages.
- Document a single rounding policy (when to round, `MidpointRounding` mode) in `docs/estimation-engine.md` and apply it everywhere.

---

## §15 Estimates

**Estimate:** Id, OrganizationId, ProjectId, EstimateNumber, Version, Status, PricingStrategy, MaterialCost, LaborCost, OtherCost, Overhead, Markup, Discount, Tax, Subtotal, GrandTotal, CreatedBy, CreatedAt, UpdatedAt, RowVersion.

**EstimateLine:** Id, EstimateId, RoomId, Category, Description, Quantity, Unit, UnitCost, UnitPrice, WastePercentage, TotalCost, TotalPrice, CalculationReference (snapshot of inputs/formula).

- Lines store price/waste/labor **snapshots**; catalog, waste or labor-rate changes never alter existing estimates.
- **Versioning:** EST-1001 v1, v2, v3… Editing a finalized estimate creates a new version; financial history is never silently modified.
- Optimistic concurrency prevents two estimators overwriting each other.

**Estimate page:** spreadsheet-style grid — Item, Room, Category, Quantity, Unit, Unit Cost, Unit Price, Waste, Total Cost, Total Price. Actions: add, edit, delete, duplicate line, recalculate, override price, add note. Totals panel: Material Cost, Labor Cost, Overhead, Markup, Tax, Grand Total (large, readable).

---

## §16 Quick estimators

Independent calculators, all using the same engines as §14:
- **Floor:** length, width, tile size, pattern, waste, tile price → area, adjusted area, tiles, boxes, material cost.
- **Wall:** wall length, wall height, openings, tile, pattern, waste.
- **Bathroom:** floor dims, wall dims, shower, backsplash, tile, materials, labor.
- **Shower:** width, depth, wall height, floor tile, wall tile, niche, bench, waterproofing, backer board, trim.
- **Backsplash:** length, height, openings, tile, pattern, waste.

---

## §17 Quotes, PDF, approval, change orders

**Quote statuses:** Draft, Sent, Viewed, Accepted, Rejected, Expired, Cancelled. A quote is a snapshot and never changes when catalog prices change.

**PDF proposal:** logo, company info, customer, project, quote number, quote date, expiration date, scope, material breakdown, labor, subtotal, discount, tax, total, terms & conditions, acceptance section.

**Customer-facing quote page:** customer can view quote, scope and price; accept; reject; request changes. Access via an unguessable, expiring token link (no customer account required). Store approval status, timestamp, IP address where appropriate, and customer identity info. Do **not** claim legally binding e-signature unless a proper signature provider is integrated.

**Sending:** through `IQuoteDeliveryProvider`/email abstraction (dev implementation may write to disk/log).

**ChangeOrder** (model now, full workflow in V2): belongs to Quote/Project; Number, Description, Reason, Amount, Status (Draft, Sent, Approved, Rejected, Cancelled), CreatedBy, ApprovedAt.

---

## §18 Audit, concurrency, soft delete

**AuditLog:** Id, OrganizationId, UserId, EntityName, EntityId, Action, OldValues, NewValues, IpAddress, UserAgent, CreatedAt.

Audit: login, logout, user changes, project create/update, estimate create/update/finalize, quote create/send/approve, price changes, catalog changes, permission changes. Never log passwords, tokens or secrets.

**Concurrency:** `rowversion` on financial records (Estimate, Quote, PriceList, etc.).

**Soft delete:** `IsDeleted`, `DeletedAt`, `DeletedBy` on business records. Financial and audit records are never physically deleted.

---

## §19 API, errors, observability, config

**REST examples:**
```
POST /api/auth/register | /api/auth/login | /api/auth/refresh
GET|POST /api/customers        GET|PUT /api/customers/{id}
GET|POST /api/projects         GET /api/projects/{id}
POST /api/projects/{id}/rooms
POST /api/takeoffs/calculate
GET|POST /api/estimates        POST /api/estimates/{id}/calculate | /finalize
GET|POST /api/quotes           POST /api/quotes/{id}/send | /approve
GET /api/audit-logs
```
Paginate list endpoints. Consistent response and error shapes.

**Errors:** centralized exception handling returning RFC 7807 ProblemDetails with `traceId`/correlation ID. Never expose SQL errors, stack traces, connection strings or internals.
```json
{ "type": "...", "title": "Validation error", "status": 400, "traceId": "...", "errors": {} }
```

**Observability:** structured logging, correlation IDs, request logging, error logging, audit logging, health checks at `/health` and `/health/ready`.

**Config:** strongly typed options (JwtSettings, DatabaseSettings, StorageSettings, EmailSettings, ApplicationSettings). No committed secrets; user-secrets/env vars locally; Azure Key Vault-ready for production.

**Performance:** normal CRUD < 500 ms; estimate calculations synchronous and fast; pagination; indexes; no N+1; projection DTOs; caching only where it clearly helps.

---

## §20 Future AI extension points (interfaces only in MVP)

`IDocumentTakeoffProvider`, `ITakeoffExtractionProvider`, `IExternalPricingProvider`, `INotificationProvider`, `IFileStorageProvider`, `IQuoteDeliveryProvider`. MVP implementations are manual/deterministic or explicit "not available" providers. No AI packages.

Future pipeline:
```
PDF/Image → Document Parser → AI Takeoff Provider → Structured Takeoff
→ Validation → Human Review → Takeoff Engine → Cost Engine → Pricing Engine → Estimate
```
AI is an **input provider** only. It never bypasses or replaces the deterministic engines, which remain the source of truth for all quantities and money.

---

## §21 Frontend

```
src/app/
  core/      auth/ guards/ interceptors/ services/ models/
  shared/    components/ dialogs/ forms/ tables/ pipes/
  features/  dashboard/ customers/ projects/ rooms/ catalog/ takeoff/
             estimates/ quotes/ reports/ settings/ users/
```
Main nav: Dashboard, Projects, Estimates, Quotes, Customers, Catalog, Reports, Settings.

UX priorities for contractors: fast data entry, clear totals, large readable numbers, minimal screens, spreadsheet-style estimate editing, desktop-first but tablet-friendly.

**Reports:** Estimate Summary, Material Takeoff, Labor Summary, Cost Breakdown, Profit/Margin, Quote Summary, Customer Proposal, Purchase List.

---

## §22 Database, seed data, testing

**Database rules:** PKs, FKs, unique constraints, check constraints where useful, indexes (always lead tenant indexes with `OrganizationId`), `CreatedAt/UpdatedAt/CreatedBy/UpdatedBy`, UTC timestamps, decimal money.

**Core relationships:**
```
Organization ─┬ Members, Customers, Projects, Tiles, Materials, Suppliers,
              └ Assemblies, LaborRates, Estimates, Quotes, AuditLogs
Project ──── Rooms, Documents, Takeoffs, Estimates, Quotes
Estimate ─── EstimateLines      Quote ─── QuoteLines
Assembly ─── AssemblyItems      PriceList ─── PriceListItems
```

**Seed:** roles, permissions, patterns, default material categories, room types, project statuses, quote statuses. Demo org "Demo Tile Company" with a customer, project, rooms, tiles, materials, labor rates, assembly, estimate and quote (development only).

**Unit tests:** floor area, wall area, openings, waste, tile quantity, box rounding, thinset, grout, backer board, waterproofing, labor (both methods), markup, margin, tax, discounts.

**Integration tests:** registration, login, refresh, tenant isolation, customer/project/estimate creation, estimate calculation, quote creation, quote approval.

**Security tests:** cross-tenant access, unauthorized endpoints, invalid JWT, expired JWT, role restrictions, permission restrictions.

---

## §23 Coding standards

Use: SOLID, clean code, DI, async/await with `CancellationToken`, nullable reference types, explicit DTOs, domain validation, FluentValidation, structured logging, XML docs on important public APIs, named constants (no magic numbers), no duplicated business logic.

Avoid: fat controllers, business logic in controllers, exposing EF entities via API, generic repositories that add no value, massive service classes, static global state, and hard-coded tenant IDs, prices, waste values or labor rates.

---

## §24 CI/CD and deployment

CI: restore → build → unit tests → integration tests (where configured) → frontend tests → Angular build → publish artifacts (GitHub Actions unless told otherwise).

Target deployment: Angular → Azure Static Web Apps (or equivalent); API → Azure App Service; DB → Azure SQL; files → Azure Blob Storage.

---

## §25 Documentation and ADRs

`/docs`: architecture.md, database.md, erd.md, api.md, authentication.md, authorization.md, multi-tenancy.md, estimation-engine.md, pricing-engine.md, quote-engine.md, development.md, deployment.md, testing.md, future-ai.md.

`README.md`: product, architecture, setup, database, running backend, running frontend, testing, environment variables.

`/docs/adr/` — each ADR lists the rejected options and why:
ADR-001 Modular Monolith vs Microservices · ADR-002 Clean Architecture · ADR-003 SQL Server · ADR-004 JWT/OIDC Authentication · ADR-005 Organization-based Multi-tenancy · ADR-006 Deterministic Calculation Engine · ADR-007 Assemblies · ADR-008 Estimate Versioning · ADR-009 Snapshot Pricing · ADR-010 Future AI Extension.

---

## §26 MVP definition of done

The MVP is complete when this works end-to-end (and is covered by an automated test):

Register → Create Organization → Login → Create Customer → Create Project → Create Bathroom → Enter floor dimensions → Enter wall dimensions → Select tile → Select pattern → Apply waste → Calculate tile, thinset, grout, waterproofing, backer board, labor → Apply markup → Apply tax → Generate Estimate → Convert to Quote → Generate PDF → Send Quote → Customer approves → Audit record created.

---

## §27 Roadmap (design extension points only — do not build)

- **V2:** change-order workflow, customer portal, supplier price imports, advanced reports, email integration, job costing.
- **V3:** PDF manual takeoff, blueprint viewer, drawing measurements, document management.
- **V4:** AI document takeoff, AI room detection, AI tile/material identification, AI estimator assistant, AI-generated scope.
- **V5:** scheduling, crew management, purchase orders, invoicing, payments, mobile app.
