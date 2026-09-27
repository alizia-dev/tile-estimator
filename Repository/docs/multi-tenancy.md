# Multi-tenancy

Implements SPEC §4. The rule is simple and absolute: **a user in organization A must never read
or modify organization B's data.**

The design goal is that isolation is *structural*, not something each handler has to remember.
Nobody writing a new endpoint should be able to forget it.

---

## How the tenant is resolved

An `OrganizationId` in a request body is **never** trusted. On every request:

1. `TenantResolutionMiddleware` reads the user id from the validated JWT.
2. It takes the requested organization from the `X-Organization-Id` header, or from the `org`
   claim if the header is absent.
3. It calls `AuthService.ResolveMembershipAsync`, which looks for an **active membership** row
   linking that user to that organization.
4. Only if one exists does it populate `CurrentOrganizationService` with the id and the
   permissions from that membership's role.

A forged header therefore gains nothing: without a membership row, nothing is set, and every
query that follows returns an empty set.

Permissions are read from the database on each request rather than from the token, so a role
change or a deactivation takes effect on the next request instead of lingering until the access
token expires.

---

## How isolation is enforced

### Global query filters

`ApplicationDbContext.ApplyGlobalFilters` walks every entity type at model-build time and adds a
filter to each one implementing `ITenantOwned`:

```csharp
e => TenantFilterBypassed || e.OrganizationId == CurrentOrganizationId
```

and, for anything soft-deletable, `&& !e.IsDeleted`.

Conceptually every query becomes `WHERE OrganizationId = @current AND IsDeleted = 0`. A handler
that writes `db.Customers.FirstOrDefault(c => c.Id == id)` is already safe: an id belonging to
another tenant simply does not exist as far as that query is concerned.

The filters are applied through generic helper methods with real lambdas rather than hand-built
expression trees, because that is the form EF Core recognises for per-instance context state.

### SaveChanges interceptor

`TenantSaveChangesInterceptor` runs on every save and:

1. stamps `CreatedAt`/`CreatedBy` and `UpdatedAt`/`UpdatedBy` in UTC (and prevents `CreatedAt`
   from ever being rewritten);
2. stamps `OrganizationId` on new tenant-owned rows from the current request;
3. **rejects** any insert or update carrying a different organization, comparing against the
   value originally loaded from the database so reassigning the property in memory cannot move a
   row between tenants;
4. converts a delete of a soft-deletable entity into a soft delete.

A write with no active organization and no explicit bypass is rejected outright rather than
written loose.

Rejections throw `TenantViolationException`, are logged with both organization ids, and surface
to the caller as a flat "You do not have access to that item."

### The bypass

Three situations legitimately run outside a tenant:

- **registration**, which creates the organization it will belong to;
- **the public quote page**, where the customer has no account;
- **seeding and background jobs**.

`ICurrentOrganizationService.BypassTenantFilter()` returns a disposable scope. It is nest-counted,
so an inner scope cannot end an outer one early, and the filter is restored on dispose.

Every use is deliberate and local. The public quote endpoints, which are the only
internet-facing ones, additionally authenticate by matching a hashed unguessable token and return
a DTO containing no cost, no margin and no internal identifiers.

---

## Indexing

Every tenant-owned index leads with `OrganizationId`, so it is usable for the filtered queries
the application actually issues:

```
IX_Customers_OrganizationId_CustomerNumber   (unique)
IX_Projects_OrganizationId_Status
IX_Estimates_OrganizationId_EstimateNumber_Version   (unique, filtered on IsDeleted = 0)
```

### Documented exceptions

Two indexes deliberately do not lead with `OrganizationId`:

- **`IX_Quotes_PublicTokenHash`** — the public quote page resolves a quote by token alone, with
  no tenant context. Unique and filtered to non-null.
- **`IX_Users_NormalizedEmail`** — a user is not tenant-owned. One account per email address
  across the system; belonging to more organizations is expressed by membership rows, not by
  registering again.

`NumberSequence` carries an `OrganizationId` but does not implement `ITenantOwned`, so it has no
query filter. It is only ever read by `NumberSequenceService`, which always filters explicitly
and takes an `UPDLOCK` so two concurrent requests cannot draw the same number.

---

## Tests

`TenantIsolationTests` runs against a real `ApplicationDbContext` with the filters and
interceptor in place, and proves:

- a query returns only the current organization's rows;
- another tenant's row cannot be fetched **even by its exact id**;
- a write stamped with another organization is rejected;
- a new row is stamped automatically;
- a write with no active organization is refused;
- moving an existing row into another tenant is rejected;
- deleting hides the row but keeps it, flagged, with a timestamp;
- switching the active organization switches the visible rows;
- a bypass sees everything and ends with its scope.
