# Estimation engine

Implements SPEC §11 and §14. All of it lives in `TileEstimator.Application/Engines`, is pure and
synchronous, and touches neither the database nor the network. An architecture test fails the
build if anything in that namespace gains a dependency on EF Core or Infrastructure.

Every quantity and every dollar in the product comes from here, including the five quick
calculators in §16. No formula is implemented twice.

---

## Rounding policy

One policy, applied everywhere. It lives in `Domain/ValueObjects/Rounding.cs`.

| What | Decimals | Mode |
|---|---|---|
| Money | 2 | `MidpointRounding.AwayFromZero` |
| Quantities, areas, rates, percentages | 4 | `MidpointRounding.AwayFromZero` |
| Purchase quantities | whole units | `Math.Ceiling` |

Rules:

- **`decimal` only.** Never `double` or `float` for money, or for a quantity that feeds money. An
  architecture test scans the domain for a `double`/`float` property whose name looks like money
  or a quantity, and fails if it finds one.
- **Round at the boundary of a step, not mid-expression.** `quantity × unitPrice` is computed at
  full precision and rounded once, so a long chain does not accumulate half-cent drift.
- **`AwayFromZero`, not banker's rounding.** A contractor checking the arithmetic by hand expects
  0.125 to become 0.13.
- **Purchase quantities always round up.** You cannot buy 20.16 boxes of tile or 2.12 bags of
  thinset. Both the calculated and the purchase quantity are stored, so the estimate can show
  "20.16 → 21 boxes" rather than silently presenting the rounded figure as exact.

Database precisions follow the same split: `decimal(18,2)` for money, `decimal(18,4)` for
quantities, rates and percentages.

---

## Takeoff engine

`ITakeoffEngine` answers "how much material is required?".

```
Floor area       = Length × Width
Wall area        = Length × Height              (one surface per wall run)
Opening area     = Width × Height × Quantity
Net area         = Gross − Σ deductions + Σ additions
Adjusted area    = Net × (1 + Waste%)
Tiles            = Adjusted ÷ CoverageSqFtPerTile
Boxes (calc)     = Adjusted ÷ SqFtPerBox
Boxes (purchase) = ceiling(Boxes calc)
Material qty     = Adjusted ÷ CoveragePerUnit × Factor
Trim qty         = TrimLinearFeet × Factor
```

### Openings deduct or add

A door or window is cut around, so its area is **deducted**. A niche or bench is tiled, so its
area is **added**. `Opening.AddsArea` decides which, defaulting from the opening type; the
estimator can override it.

Deducting more than the surface contains is rejected with a message naming the surface, rather
than quietly producing a negative area.

### Waste resolution (§11)

In order, first match wins:

1. **Estimator override** on the surface
2. **Best-matching waste rule** — most specific match, ties broken by `Priority` (higher wins)
3. **Pattern default**
4. **Zero**

A rule's null field means "any". Specificity is the count of fields it pins down, so a rule for
"marble on a shower wall" beats one for "marble anywhere".

There is **no hard-coded industry-standard waste**. When nothing is configured the answer is 0%,
and the result says so. Every default ships as seed data the organization owns and can edit.

The resolution is returned with the result (`WasteSource` plus a human-readable explanation), so
the UI can tell an estimator *why* a surface came out at 15%.

### Tile is priced by the square foot but bought by the box

The billable amount for a tile line is the square footage of the **whole boxes purchased**, not
the square footage installed. A contractor pays for the full box.

```
21 boxes × 10 SF/box = 210 SF billable, for 201.6 SF of adjusted area
```

### Assembly expansion (§12)

Applying an assembly to a surface expands it into material and labor lines. Each item derives
its quantity from one of four methods:

| Method | Quantity |
|---|---|
| `PerArea` | adjusted area × factor |
| `PerLinearFoot` | trim linear feet × factor |
| `PerEach` | fixed quantity × factor |
| `PerCoverage` | adjusted area ÷ coverage × factor |

Coverage comes from the material's catalog row, or from a `CoverageOverride` on the assembly
item. A `PerCoverage` item with no coverage anywhere is an **error**, not a zero — silently
producing "0 bags of thinset" would be worse than refusing.

`Factor` handles things like two coats of waterproofing membrane.

Bags, boxes, sheets, rolls and tubes round up to whole units. Square feet, linear feet and hours
are cut to size and stay fractional.

### Labor (§13)

Two methods, chosen per rate:

```
UnitRate:      Quantity × Rate
               450 SF × $8.00/SF = $3,600.00

Productivity:  Quantity ÷ Productivity × HourlyRate
               450 SF ÷ 50 SF/hr = 9 hr × $65/hr = $585.00
```

Labor lines carry no waste percentage: waste is a material concept.

### Reference case (§14)

Covered by `ReferenceCase_12x15_with_12_percent_waste_needs_21_boxes`:

| Step | Value |
|---|---|
| Room | 12 ft × 15 ft |
| Gross area | 180 SF |
| Openings | none |
| Net area | 180 SF |
| Waste | 12% |
| Adjusted area | 201.6 SF |
| Tile | 10 SF per box |
| Boxes calculated | 20.16 |
| **Boxes to purchase** | **21** |

Verified end to end against the running API as well as in the unit tests.

---

## Cost engine

`ICostCalculationEngine` answers "what does the job cost us?". It groups extended costs by
category, adds any costs entered directly (equipment, delivery, dump fees), and applies overhead
as a percentage of direct cost.

```
Direct cost = material + labor + equipment + delivery + other
Overhead    = direct cost × Overhead%
Total cost  = direct cost + overhead
```

**No markup logic lives here.** A line carries a selling price alongside its cost, and the cost
engine ignores it; a unit test asserts exactly that.

---

## Pricing engine

See [pricing-engine.md](pricing-engine.md).

---

## Calculation breakdown

Every result carries a `CalculationBreakdown`: labelled steps plus a one-line formula written the
way a contractor would check it.

```
201.6 SF / 10 SF per box = 20.16 -> 21 boxes = 210 SF at $5.00/SF
450 SF x $8.00/SF = $3,600.00
201.6 SF / 95 SF per BAG = 2.1221 BAG
```

The formula is stored on the estimate line as `CalculationReference`, so an estimate opened two
years later still explains itself without recomputing anything.
