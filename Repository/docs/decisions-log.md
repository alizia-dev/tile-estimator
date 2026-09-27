# Decisions log

Choices made where SPEC.md left room, recorded as CLAUDE.md §4 requires. Anything that would
change the data model or money calculations significantly is flagged for review.

---

## Patterns and waste rules are per-organization copies

**Ambiguity.** SPEC §11 requires waste to be organization-configurable but does not say whether
patterns are global seed data or per-tenant rows.

**Decision.** Per-organization. `Pattern` and `WasteRule` are `TenantEntity`, and
`OrganizationProvisioner` creates a full set at registration.

**Why.** If patterns were global, one contractor editing "Herringbone" to 22% would change it for
everyone. Per-tenant copies make "nothing hard-coded that an organization should configure"
actually true.

---

## A user may belong to several organizations

**Decision.** `User` is not tenant-owned. Membership is a separate row, and one email address has
one account.

**Selection.** The client may send `X-Organization-Id`; the server resolves membership and
ignores anything it cannot verify. The shell shows an organization switcher only when there is
more than one.

---

## Surfaces model both plan and elevation

**Ambiguity.** SPEC §9 lists surface types but not how to measure them.

**Decision.** One `Surface` carries `LengthFeet` plus *either* `WidthFeet` (floors) or
`HeightFeet` (walls). `AreaOverrideSquareFeet` handles irregular shapes and wins over the
dimensions. Trim is `TrimLinearFeet`, because trim is bought by the foot, not by area.

Several wall runs are entered either as several surfaces or as one with the runs added together;
the UI says so.

---

## Niches and benches add area; doors and windows deduct it

**Ambiguity.** §9 lists niches and benches as openings, but they are tiled, not cut around.

**Decision.** `Opening.AddsArea`, defaulted from the type and overridable. The UI colours
additions green and deductions red, because confusing them changes the tile count.

---

## Tax basis is configurable, and apportioned by the cost mix

**Ambiguity.** §14 does not say whether tax applies to materials, labor or both.

**Decision.** `TaxBasis` per organization, defaulting to `MaterialsAndLabor`. US states differ,
so the product cannot pick for them.

**How the share is derived.** The priced subtotal is apportioned by the material/labor mix of the
*cost*. Taxing cost directly would under-charge; apportioning price keeps tax proportional to
what the customer is billed. **Flagged for review** — it affects money, and a tax specialist
should confirm it against a couple of state rules.

---

## Order of operations: cost → markup → discount → tax

**Decision.** Documented in [pricing-engine.md](pricing-engine.md), with `DiscountBeforeTax`
configurable and defaulting to `true` (tax on the discounted subtotal, so the customer is taxed
on what they pay).

---

## Gross profit is measured after the discount

**Decision.** A 50% markup then a 20% discount reports $200 of profit on $1,000 of cost, not
$500. The estimator needs the number they will actually make.

---

## Tile is billed by whole boxes

**Ambiguity.** §14 requires both calculated and purchase quantities, but not which one is
charged.

**Decision.** The billable amount is the square footage of the whole boxes purchased. A
contractor pays for the full box.

**Flagged.** Some contractors bill installed square footage and absorb the offcut. If customers
ask, this becomes an organization setting.

---

## Estimate versions share a number, with a unique index on (number, version)

**Decision.** `EST-1001 v1` and `v2` coexist. Finalizing v1 and revising it produces v2 and marks
v1 `Superseded`.

---

## Public quote links are hashed, expiring tokens

**Decision.** 256-bit URL-safe token, SHA-256 hashed at rest, expiring at the quote expiry plus a
configurable grace period (default 7 days).

**Re-sending rotates the token**, so a link forwarded from an earlier send stops working. Unknown,
expired, revoked and cancelled all return the same flat 404.

---

## Numbering uses a per-organization counter with a row lock

**Decision.** `NumberSequence` keyed by `(OrganizationId, EntityType)`, read with `UPDLOCK,
HOLDLOCK` and incremented in the same transaction. Prefixes are configurable. The unique index on
the document table is the backstop if a counter is ever restored out of step.

---

## Registration runs through the execution strategy

**Problem found in testing.** SQL Server retry-on-failure refuses a user-initiated transaction.

**Decision.** `IApplicationDbContext.ExecuteInTransactionAsync` wraps the whole unit in the
provider execution strategy, so a transient failure retries the *entire* registration rather than
leaving a half-provisioned tenant. Ids are generated before the block so a retry reuses them, and
the verification email is sent only after the commit.

---

## New child entities are tracked through the DbSet, never the parent navigation

**Bug found in testing, twice.** Adding a new entity to a tracked parent navigation *and* to the
`DbSet` made EF fix the same instance into the collection twice, doubling the estimate cost.
Adding it only to the navigation left its state to change detection, which issued UPDATE
statements for rows that did not exist yet.

**Decision.** New children go through `db.Set.Add`, and the navigation is refreshed from the
database afterwards. Both failure modes are now impossible.

---

## Invariant globalization is off

**Decision.** The SQL Server client requires ICU. Money and quantities are still formatted with
`InvariantCulture` explicitly, so output does not depend on the host locale.

---

## Solution file is `.slnx`

.NET 10 generates the XML solution format. `CLAUDE.md` refers to `TileEstimator.sln`; the working
commands in the README use `TileEstimator.slnx`.

---

## Open for the product owner

1. **Tax apportionment** — confirm against real state rules (flagged above).
2. **Billing whole boxes vs installed SF** — confirm, or make it a setting.
3. **CSV catalog import** — §10 asks for it behind an interface. The interface boundary exists in
   the catalog service; the import endpoint and preview UI are not built. Listed as a known gap.
4. **QuestPDF licence** — Community covers under $1M revenue. See
   [ADR-011](adr/ADR-011-questpdf.md).
