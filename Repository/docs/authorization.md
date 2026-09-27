# Authorization

Implements SPEC §6. Role-based, with granular permissions and policy-based enforcement.

The matrix below is the single source of truth. It is declared once in
`TileEstimator.Application/Authorization/Permissions.cs`, seeded into the database from there,
and read by both the API policies and the Angular permission directive.

---

## Roles

| Role | Who they are |
|---|---|
| **Owner** | The person who registered. Full control; an organization must always keep one. |
| **Admin** | Runs the office: people, catalog, settings. |
| **Estimator** | Builds takeoffs, estimates and pricing. |
| **Sales** | Owns the customer relationship and the quote. |
| **Installer** | Reads scope and material lists on site. |
| **Viewer** | Read-only. |

---

## Matrix

| Permission | Owner | Admin | Estimator | Sales | Installer | Viewer |
|---|:-:|:-:|:-:|:-:|:-:|:-:|
| `project.read` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `project.create` | ✓ | ✓ | ✓ | ✓ | | |
| `project.update` | ✓ | ✓ | ✓ | ✓ | | |
| `project.delete` | ✓ | ✓ | | | | |
| `estimate.read` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `estimate.create` | ✓ | ✓ | ✓ | | | |
| `estimate.update` | ✓ | ✓ | ✓ | | | |
| `estimate.delete` | ✓ | ✓ | ✓ | | | |
| `estimate.approve` | ✓ | ✓ | ✓ | | | |
| `quote.read` | ✓ | ✓ | ✓ | ✓ | | ✓ |
| `quote.create` | ✓ | ✓ | ✓ | ✓ | | |
| `quote.send` | ✓ | ✓ | | ✓ | | |
| `quote.approve` | ✓ | ✓ | | ✓ | | |
| `customer.read` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `customer.manage` | ✓ | ✓ | ✓ | ✓ | | |
| `catalog.read` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `catalog.manage` | ✓ | ✓ | ✓ | | | |
| `users.read` | ✓ | ✓ | | | | |
| `users.invite` | ✓ | ✓ | | | | |
| `users.manage` | ✓ | ✓ | | | | |
| `settings.read` | ✓ | ✓ | ✓ | | | |
| `settings.manage` | ✓ | ✓ | | | | |
| `audit.read` | ✓ | ✓ | | | | |
| `reports.read` | ✓ | ✓ | ✓ | ✓ | | ✓ |

### Why the lines fall where they do

- **Sales can send and answer a quote but not re-price one.** Sending is the customer-facing act;
  changing the margin is not their call.
- **Estimators own the catalog** because waste percentages, coverages and labor rates are the raw
  material of an estimate, and needing an admin to change one would make estimating slower.
- **Estimators cannot manage people or settings.** Those are administrative.
- **Installers see scope and material lists, never pricing controls.** They get
  `project.read`, `estimate.read`, `customer.read` and `catalog.read` and nothing else.
- **Only Owner and Admin read the audit log**, since it records who did what.

---

## Enforcement

### Server

```csharp
[HasPermission(Permissions.EstimateUpdate)]
public async Task<ActionResult<EstimateResponse>> UpdatePricing(...)
```

`PermissionPolicyProvider` creates a policy per permission code on demand, so adding a permission
needs no startup registration. `PermissionAuthorizationHandler` checks it against the membership
resolved for **this request**, not against the claims in the token, so a role change takes effect
immediately.

The API has a **fallback policy requiring an authenticated user**, so a new endpoint is protected
by default and must opt out explicitly with `[AllowAnonymous]`. The only endpoints that do are
the auth endpoints, the health checks and the public quote page.

### Client

The Angular app uses the same codes:

```html
<button *teHasPermission="'estimate.update'">Edit</button>
```

plus `permissionGuard(...)` on routes and a filtered nav list.

**This hides UI, it does not secure anything.** Every endpoint enforces the same permission
independently; bypassing the client gains a broken-looking page and a 403.

---

## Guardrails

- An organization must keep **at least one active Owner**. Demoting or deactivating the last one
  returns 409 with an explanation rather than leaving the tenant unadministrable.
- Permission and role changes are written to the audit log as `PermissionChange`.
- A deactivated membership fails tenant resolution, so the user loses access on their next
  request without needing their token revoked.
