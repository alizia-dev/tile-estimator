# API

Base path `/api`. JSON in, JSON out. Enums travel as strings.

Swagger UI is served at `/swagger` in development.

---

## Conventions

**Authentication.** `Authorization: Bearer <access token>`. Optional `X-Organization-Id` header
selects the organization for a user who belongs to several; membership is re-verified server-side
on every request regardless (see [multi-tenancy.md](multi-tenancy.md)).

**Authorization.** The permission column below is enforced by policy. See
[authorization.md](authorization.md).

**Errors.** Every failure is RFC 7807 ProblemDetails carrying `traceId`.

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation error",
  "status": 400,
  "traceId": "0HNOOTE0FT538:00000001",
  "errors": { "marginPercentage": ["Margin must be at least 0% and below 100%."] }
}
```

| Status | Meaning |
|---|---|
| 400 | validation or a domain rule |
| 401 | missing, invalid or expired token |
| 403 | authenticated but not permitted — **also what a cross-tenant attempt returns** |
| 404 | not found, or not yours |
| 409 | conflict: finalized estimate, concurrency, duplicate SKU |
| 429 | rate limited |

Stack traces, SQL and connection strings never appear in a response.

**Paging.** List endpoints take `page`, `pageSize` (max 200), `search`, `sortBy`,
`sortDescending`, and return `{ items, page, pageSize, totalCount, totalPages, hasPrevious, hasNext }`.

---

## Auth — `/api/auth` (rate limited, 10/min/IP)

| Method | Route | Permission | Notes |
|---|---|---|---|
| POST | `/register` | anonymous | User + organization + Owner membership + defaults, in one transaction |
| POST | `/login` | anonymous | Same message for wrong password, unknown email and deactivated account |
| POST | `/refresh` | anonymous | Rotates the token; replaying a revoked one revokes the whole family |
| POST | `/logout` | authenticated | |
| GET | `/me` | authenticated | Profile, active organization, permissions |
| POST | `/verify-email` | anonymous | |
| POST | `/resend-verification` | anonymous | Always 202 |
| POST | `/forgot-password` | anonymous | Always 202 — cannot enumerate accounts |
| POST | `/reset-password` | anonymous | Signs out every session |
| POST | `/change-password` | authenticated | Signs out every other session |

## Organization — `/api/organization`

| Method | Route | Permission |
|---|---|---|
| GET | `/settings` | `settings.read` |
| PUT | `/profile` | `settings.manage` |
| PUT | `/settings` | `settings.manage` |
| GET | `/members` | `users.read` |
| PUT | `/members/{id}` | `users.manage` |
| GET | `/invitations` | `users.read` |
| POST | `/invitations` | `users.invite` |
| DELETE | `/invitations/{id}` | `users.invite` |

## Customers — `/api/customers`

| Method | Route | Permission |
|---|---|---|
| GET | `/` | `customer.read` |
| GET | `/{id}` | `customer.read` |
| POST | `/` | `customer.manage` |
| PUT | `/{id}` | `customer.manage` |
| DELETE | `/{id}` | `customer.manage` — 409 while projects reference them |

## Projects — `/api/projects`

| Method | Route | Permission |
|---|---|---|
| GET | `/` | `project.read` |
| GET | `/{id}` | `project.read` |
| POST | `/` | `project.create` |
| PUT | `/{id}` | `project.update` |
| PUT | `/{id}/status` | `project.update` |
| DELETE | `/{id}` | `project.delete` — 409 once quotes exist |
| GET | `/{id}/rooms` | `project.read` |
| POST | `/{id}/rooms` | `project.update` |
| PUT/DELETE | `/{id}/rooms/{roomId}` | `project.update` |
| POST | `/{id}/rooms/{roomId}/surfaces` | `project.update` |
| PUT/DELETE | `/{id}/rooms/{roomId}/surfaces/{surfaceId}` | `project.update` |
| POST | `/{id}/surfaces/{surfaceId}/openings` | `project.update` |
| PUT/DELETE | `/{id}/surfaces/{surfaceId}/openings/{openingId}` | `project.update` |
| GET/POST | `/{id}/notes` | `project.read` / `project.update` |

## Catalog — `/api/catalog`

Read needs `catalog.read`; create, update and delete need `catalog.manage`.

`/tiles`, `/materials`, `/patterns`, `/waste-rules`, `/labor-rates`, `/assemblies`, `/suppliers`.

A duplicate SKU returns 400 with a field-level message. Deleting a material or labor rate an
assembly uses returns 409 suggesting deactivation instead.

## Takeoff — `/api/takeoffs` (all `estimate.read`)

| Method | Route | Notes |
|---|---|---|
| POST | `/calculate?projectId=` | Every surface in the project |
| POST | `/calculate-surface?surfaceId=` | One surface |
| POST | `/quick/floor` | §16 |
| POST | `/quick/wall` | §16 |
| POST | `/quick/backsplash` | §16 |
| POST | `/quick/shower` | §16 — pan, wall run, niche, bench |
| POST | `/quick/bathroom` | §16 — floor, walls, shower, backsplash |

Every one of these runs the same `ITakeoffEngine`. Each returned line carries a `calculation`
string showing its arithmetic.

## Estimates — `/api/estimates`

| Method | Route | Permission |
|---|---|---|
| GET | `/` , `/{id}` , `/{id}/versions` | `estimate.read` |
| POST | `/` | `estimate.create` |
| POST | `/{id}/calculate` | `estimate.update` |
| POST | `/{id}/rebuild` | `estimate.update` — replaces lines from the project |
| PUT | `/{id}/pricing` | `estimate.update` — accepts `rowVersion`, 409 on conflict |
| PUT | `/{id}/details` | `estimate.update` |
| POST | `/{id}/lines` | `estimate.update` |
| PUT/DELETE | `/{id}/lines/{lineId}` | `estimate.update` |
| POST | `/{id}/lines/{lineId}/duplicate` | `estimate.update` |
| POST/DELETE | `/{id}/lines/{lineId}/override-price` | `estimate.update` |
| POST | `/{id}/finalize` | `estimate.approve` |
| POST | `/{id}/new-version` | `estimate.create` |
| DELETE | `/{id}` | `estimate.delete` — 409 if finalized |

Editing anything on a finalized estimate returns **409**. Create a new version instead.

## Quotes — `/api/quotes`

| Method | Route | Permission |
|---|---|---|
| GET | `/` , `/{id}` | `quote.read` |
| POST | `/` | `quote.create` — 409 unless the estimate is finalized |
| PUT | `/{id}/content` | `quote.create` — 409 once sent |
| GET | `/{id}/pdf` | `quote.read` |
| POST | `/{id}/send` | `quote.send` — issues a fresh link, retiring the old one |
| POST | `/{id}/record-decision` | `quote.approve` — for a reply by phone |
| POST | `/{id}/cancel` | `quote.create` |

## Public quote — `/api/public/quotes` (anonymous, 30/min/IP)

The only internet-facing surface. The token is the sole credential and is matched against a
stored SHA-256 hash.

| Method | Route |
|---|---|
| GET | `/{token}` — marks Viewed on first open |
| GET | `/{token}/pdf` |
| POST | `/{token}/respond` — Accepted, Rejected or ChangesRequested |

An unknown, expired, revoked or cancelled token all return the same flat **404**, so the endpoint
cannot be probed to learn which tokens exist. The response DTO contains no cost, no margin and no
internal identifiers.

## Dashboard, audit and health

| Method | Route | Permission |
|---|---|---|
| GET | `/api/dashboard` | `project.read` |
| GET | `/api/audit-logs` | `audit.read` |
| GET | `/health` | anonymous |
| GET | `/health/ready` | anonymous — includes the database |
