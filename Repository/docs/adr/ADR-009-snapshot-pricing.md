# ADR-009: Estimate and quote lines store snapshots

Status: Accepted

## Context

Catalog prices, waste percentages and labor rates change constantly. A quote sent in March must
still total the same in September.

## Decision

Every estimate line stores the `UnitCost`, `UnitPrice`, `WastePercentage` and
`CalculationReference` used when it was calculated. Every quote line stores its price, and the
quote additionally snapshots the customer and company details.

Recalculating an estimate reads those stored line values. It does **not** go back to the catalog.
Only an explicit `rebuild` re-expands from the project, and the UI warns that overrides will be
lost.

Catalog links on a line are kept for reporting only, with `DeleteBehavior.NoAction`, so removing
a tile can never rewrite or delete a historical line.

## Rejected

**Referencing the catalog and computing on read.** Opening an old estimate would show today
prices, which is wrong and, for an accepted quote, indefensible.

**Effective-dated price lookups.** They would reconstruct the March price, but not an estimator
manual override, and every read would pay for the join.

## Consequences

- An old estimate reproduces exactly, including its arithmetic.
- Storage is slightly larger. Worth it.
- Verified end to end: the workflow test raises a tile from $2.50 to $99.00/SF and asserts the
  finalized total does not move.
